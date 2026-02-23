using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using System.Threading;

namespace ClipSave.UnitTests;

[UnitTest]
public class SavePipelineTests : IDisposable
{
    private readonly string _testDirectory;

    public SavePipelineTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_SavePipelineTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_testDirectory))
        {
            return;
        }

        Directory.Delete(_testDirectory, true);
    }

    [Fact]
    public void Dispose_WhenExecutionGateIsHeld_DoesNotDisposeGateImmediately()
    {
        var gate = FakeSaveExecutionGate.CreateHeld();
        using var pipeline = CreatePipeline(gate);

        pipeline.Dispose();

        gate.IsDisposed.Should().BeFalse("Dispose should not dispose the execution gate while it is still held");
        gate.TryDisposeIfIdleCallCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_WhenCalledAgainAfterExecutionGateBecomesIdle_DisposesGate()
    {
        var gate = FakeSaveExecutionGate.CreateHeld();
        using var pipeline = CreatePipeline(gate);

        pipeline.Dispose();
        gate.Exit();

        pipeline.Dispose();

        gate.IsDisposed.Should().BeTrue(
            "a repeated Dispose call should complete deferred execution-gate disposal once it becomes idle");
        gate.TryDisposeIfIdleCallCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_AfterDispose_ReturnsBusyWithoutTouchingExecutionGate()
    {
        var gate = new FakeSaveExecutionGate();
        using var pipeline = CreatePipeline(gate);
        pipeline.Dispose();

        var result = await pipeline.ExecuteAsync();

        result.Kind.Should().Be(SaveResultKind.Busy);
        gate.TryEnterCallCount.Should().Be(0, "disposed pipelines should short-circuit before trying to enter the gate");
    }

    [Fact]
    public async Task ExecuteAsync_WhenExecutionGateIsAlreadyHeld_ReturnsBusy()
    {
        var gate = FakeSaveExecutionGate.CreateHeld();
        using var pipeline = CreatePipeline(gate);

        var result = await pipeline.ExecuteAsync();

        result.Kind.Should().Be(SaveResultKind.Busy);
        gate.TryEnterCallCount.Should().Be(1);
    }

    private SavePipeline CreatePipeline(ISaveExecutionGate executionGate)
    {
        var settingsService = new SettingsService(
            Mock.Of<ILogger<SettingsService>>(),
            _testDirectory);
        var notificationService = new NotificationService(
            Mock.Of<ILogger<NotificationService>>(),
            settingsService);
        var imageEncodingService = new ImageEncodingService(
            Mock.Of<ILogger<ImageEncodingService>>());
        var contentEncodingService = new ContentEncodingService(
            Mock.Of<ILogger<ContentEncodingService>>(),
            imageEncodingService);
        var fileStorageService = new FileStorageService(
            Mock.Of<ILogger<FileStorageService>>());
        var clipboardService = new ClipboardService(
            Mock.Of<ILogger<ClipboardService>>());
        var activeWindowService = new ActiveWindowService(
            Mock.Of<ILogger<ActiveWindowService>>());
        var localizationService = new LocalizationService(
            Mock.Of<ILogger<LocalizationService>>());

        return new SavePipeline(
            Mock.Of<ILogger<SavePipeline>>(),
            clipboardService,
            contentEncodingService,
            fileStorageService,
            notificationService,
            settingsService,
            activeWindowService,
            localizationService,
            executionGate);
    }

    private sealed class FakeSaveExecutionGate : ISaveExecutionGate
    {
        private int _held;
        private int _disposed;

        public int TryEnterCallCount { get; private set; }
        public int ExitCallCount { get; private set; }
        public int TryDisposeIfIdleCallCount { get; private set; }
        public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

        public FakeSaveExecutionGate()
        {
        }

        private FakeSaveExecutionGate(bool isHeld)
        {
            _held = isHeld ? 1 : 0;
        }

        public static FakeSaveExecutionGate CreateHeld()
        {
            return new FakeSaveExecutionGate(isHeld: true);
        }

        public bool TryEnter()
        {
            ThrowIfDisposed();
            TryEnterCallCount++;
            return Interlocked.CompareExchange(ref _held, 1, 0) == 0;
        }

        public void Exit()
        {
            ThrowIfDisposed();
            ExitCallCount++;

            if (Interlocked.CompareExchange(ref _held, 0, 1) != 1)
            {
                throw new SemaphoreFullException("Execution gate is not held.");
            }
        }

        public bool TryDisposeIfIdle()
        {
            ThrowIfDisposed();
            TryDisposeIfIdleCallCount++;

            if (Volatile.Read(ref _held) == 1)
            {
                return false;
            }

            Interlocked.Exchange(ref _disposed, 1);
            return true;
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(FakeSaveExecutionGate));
            }
        }
    }
}
