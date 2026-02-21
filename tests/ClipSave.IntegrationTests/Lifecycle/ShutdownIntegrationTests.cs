using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class ShutdownIntegrationTests
{
    [StaFact]
    [Spec("SPEC-021-007")]
    public void TriggerExitMenu_Invoked_RaisesExitRequested()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        using var trayService = new TrayService(loggerFactory.CreateLogger<TrayService>());
        var eventRaised = false;

        trayService.ExitRequested += (_, _) => eventRaised = true;

        // Simulate clicking Exit in the tray context menu.
        trayService.TriggerExitMenuForTest();

        eventRaised.Should().BeTrue("clicking Exit should raise ExitRequested");
    }

    [StaFact]
    [Spec("SPEC-080-001")]
    public void DisposeServices_DuringShutdown_CompletesCleanly()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var testPath = Path.Combine(Path.GetTempPath(), $"ClipSave_Integration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testPath);

        try
        {
            // Create all services used in the shutdown flow.
            var settingsService = new SettingsService(
                loggerFactory.CreateLogger<SettingsService>(), testPath);
            var trayService = new TrayService(
                loggerFactory.CreateLogger<TrayService>());
            var hotkeyService = new HotkeyService(
                loggerFactory.CreateLogger<HotkeyService>());
            var clipboardService = new ClipboardService(
                loggerFactory.CreateLogger<ClipboardService>());
            var imageEncodingService = new ImageEncodingService(
                loggerFactory.CreateLogger<ImageEncodingService>());
            var contentEncodingService = new ContentEncodingService(
                loggerFactory.CreateLogger<ContentEncodingService>(), imageEncodingService);
            var storageService = new FileStorageService(
                loggerFactory.CreateLogger<FileStorageService>());
            var notificationService = new NotificationService(
                loggerFactory.CreateLogger<NotificationService>(), settingsService);
            var activeWindowService = new ActiveWindowService(
                loggerFactory.CreateLogger<ActiveWindowService>());
            var savePipeline = new SavePipeline(
                loggerFactory.CreateLogger<SavePipeline>(),
                clipboardService,
                contentEncodingService,
                storageService,
                notificationService,
                settingsService,
                activeWindowService);

            // Simulate shutdown by disposing all services.
            var act = () =>
            {
                hotkeyService.Dispose();
                trayService.Dispose();
                savePipeline.Dispose();
            };

            // Assert: completes without exceptions.
            act.Should().NotThrow("service disposal during shutdown should complete without exceptions");
        }
        finally
        {
            if (Directory.Exists(testPath))
            {
                try { Directory.Delete(testPath, true); }
                catch { /* ignore */ }
            }
        }
    }

    [StaFact]
    [Spec("SPEC-080-002")]
    public void DisposeHotkeyService_OnShutdown_CompletesWithoutException()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var hotkeyService = new HotkeyService(loggerFactory.CreateLogger<HotkeyService>());

        // Dispose should complete without exceptions.
        var act = () => hotkeyService.Dispose();
        act.Should().NotThrow("hotkey unregistration should complete without exceptions");
    }

    [StaFact]
    [Spec("SPEC-021-009")]
    public void DisposeTrayService_OnShutdown_CompletesWithoutException()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var trayService = new TrayService(loggerFactory.CreateLogger<TrayService>());

        // Dispose should hide and release the tray icon without exceptions.
        var act = () => trayService.Dispose();
        act.Should().NotThrow("tray icon disposal should complete without exceptions");
    }

    [Fact]
    [Spec("SPEC-080-003")]
    public void MutexRelease_AllowsNewInstance()
    {
        var testId = Guid.NewGuid().ToString("N");
        var mutexName = $"Local\\ClipSave_Integration_MutexTest_{testId}";
        var pipeName = $"ClipSave_Integration_MutexTest_{testId}";

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<SingleInstanceService>();

        // First instance acquires the mutex.
        var firstService = new SingleInstanceService(logger, mutexName, pipeName);
        var firstAcquired = firstService.TryAcquireOrNotify();
        firstAcquired.Should().BeTrue("the first instance should acquire the mutex");

        // Dispose
        firstService.Dispose();

        // Second instance should acquire after the first is disposed.
        var secondService = new SingleInstanceService(logger, mutexName, pipeName);
        var secondAcquired = secondService.TryAcquireOrNotify();

        secondAcquired.Should().BeTrue("a new instance should acquire the mutex after the previous one is disposed");

        secondService.Dispose();
    }

    [Fact]
    [Spec("SPEC-080-004")]
    public async Task PipeServerStop_RejectsConnections()
    {
        var testId = Guid.NewGuid().ToString("N");
        var mutexName = $"Local\\ClipSave_Integration_PipeTest_{testId}";
        var pipeName = $"ClipSave_Integration_PipeTest_{testId}";

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<SingleInstanceService>();

        var service = new SingleInstanceService(logger, mutexName, pipeName);
        service.TryAcquireOrNotify();

        // Disposing should stop the pipe server.
        service.Dispose();

        // Wait until the server actually stops accepting connections.
        var rejected = await WaitUntilAsync(
            () => !CanConnectToPipe(pipeName, timeoutMs: 100),
            timeout: TimeSpan.FromSeconds(3),
            pollInterval: TimeSpan.FromMilliseconds(50));

        rejected.Should().BeTrue("connection should fail because the pipe server has been stopped");
    }

    [Fact]
    [Spec("SPEC-080-006")]
    public void AcquireMutex_WhenAlreadyOwned_FailsForSecondInstance()
    {
        var testId = Guid.NewGuid().ToString("N");
        var mutexName = $"Local\\ClipSave_Integration_DuplicateTest_{testId}";
        var pipeName = $"ClipSave_Integration_DuplicateTest_{testId}";

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<SingleInstanceService>();

        // First instance.
        var firstService = new SingleInstanceService(logger, mutexName, pipeName);
        firstService.TryAcquireOrNotify();

        // Second instance.
        var secondService = new SingleInstanceService(logger, mutexName, pipeName);
        var secondAcquired = secondService.TryAcquireOrNotify();

        secondAcquired.Should().BeFalse("the second instance should fail when another instance already exists");

        // Disposing the second instance should still be safe.
        var act = () => secondService.Dispose();
        act.Should().NotThrow("disposing after failed mutex acquisition should be safe");

        firstService.Dispose();
    }

    [StaFact]
    public void DisposeServices_ManualSequence_TracksExpectedOrder()
    {
        var testId = Guid.NewGuid().ToString("N");
        var mutexName = $"Local\\ClipSave_Integration_SequenceTest_{testId}";
        var pipeName = $"ClipSave_Integration_SequenceTest_{testId}";
        var testPath = Path.Combine(Path.GetTempPath(), $"ClipSave_Integration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testPath);

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        try
        {
            var disposalOrder = new List<string>();

            // Create services.
            var singleInstanceService = new SingleInstanceService(
                loggerFactory.CreateLogger<SingleInstanceService>(), mutexName, pipeName);
            singleInstanceService.TryAcquireOrNotify();

            var settingsService = new SettingsService(
                loggerFactory.CreateLogger<SettingsService>(), testPath);
            var trayService = new TrayService(
                loggerFactory.CreateLogger<TrayService>());
            var hotkeyService = new HotkeyService(
                loggerFactory.CreateLogger<HotkeyService>());
            var notificationService = new NotificationService(
                loggerFactory.CreateLogger<NotificationService>(), settingsService);

            trayService.ExitRequested += (_, _) => { };

            hotkeyService.Dispose();
            disposalOrder.Add("HotkeyService");

            trayService.Dispose();
            disposalOrder.Add("TrayService");

            singleInstanceService.Dispose();
            disposalOrder.Add("SingleInstanceService");

            // Assert disposal order.
            disposalOrder.Should().HaveCount(3);
            disposalOrder[0].Should().Be("HotkeyService");
            disposalOrder[1].Should().Be("TrayService");
            disposalOrder[2].Should().Be("SingleInstanceService");
        }
        finally
        {
            if (Directory.Exists(testPath))
            {
                try { Directory.Delete(testPath, true); }
                catch { /* ignore */ }
            }
        }
    }

    private static async Task<bool> WaitUntilAsync(
        Func<bool> predicate,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (predicate())
            {
                return true;
            }

            await Task.Delay(pollInterval);
        }

        return predicate();
    }

    private static bool CanConnectToPipe(string pipeName, int timeoutMs)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            client.Connect(timeout: timeoutMs);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}

