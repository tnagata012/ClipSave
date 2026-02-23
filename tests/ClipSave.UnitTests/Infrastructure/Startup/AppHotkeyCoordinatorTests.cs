using ClipSave.Infrastructure.Startup;
using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.IO;
using System.Windows.Threading;

namespace ClipSave.UnitTests;

[UnitTest]
public class AppHotkeyCoordinatorTests : IDisposable
{
    private readonly string _settingsDirectory;
    private readonly SettingsService _settingsService;
    private readonly HotkeyService _hotkeyService;
    private readonly AppHotkeyCoordinator _coordinator;
    private readonly List<NotificationMessage> _notifications = new();

    public AppHotkeyCoordinatorTests()
    {
        _settingsDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ClipSave_AppHotkeyCoordinatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_settingsDirectory);

        _settingsService = new SettingsService(
            Mock.Of<ILogger<SettingsService>>(),
            _settingsDirectory);
        var notificationService = new NotificationService(
            Mock.Of<ILogger<NotificationService>>(),
            _settingsService);
        notificationService.NotificationRequested += (_, notification) => _notifications.Add(notification);

        _hotkeyService = new HotkeyService(
            Mock.Of<ILogger<HotkeyService>>());

        _coordinator = new AppHotkeyCoordinator(
            _hotkeyService,
            _settingsService,
            notificationService,
            NullLogger<AppHotkeyCoordinator>.Instance,
            key => key);
    }

    public void Dispose()
    {
        _coordinator.Dispose();
        _hotkeyService.Dispose();

        if (Directory.Exists(_settingsDirectory))
        {
            try
            {
                Directory.Delete(_settingsDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    [StaFact]
    public void Initialize_CalledTwice_DoesNotThrow()
    {
        var act = () =>
        {
            _coordinator.Initialize();
            _coordinator.Initialize();
        };

        act.Should().NotThrow();
    }

    [StaFact]
    public void Initialize_WhenHotkeySettingIsInvalid_NotifiesRegistrationFailure()
    {
        _settingsService.Current.Hotkey.Modifiers = new List<string> { "UnknownModifier" };
        _settingsService.Current.Hotkey.Key = "V";

        _coordinator.Initialize();

        _notifications.Should().ContainSingle(notification =>
            notification.Message.Contains("App_HotkeyRegisterFailed", StringComparison.Ordinal));
    }

    [StaFact]
    public void HotkeyPressed_WhenServiceRaisesHotkeyMessage_ForwardsEvent()
    {
        _coordinator.Initialize();

        var raisedCount = 0;
        _coordinator.HotkeyPressed += (_, _) => raisedCount++;

        _hotkeyService.TryHandleHotkeyMessageForTest().Should().BeTrue();
        FlushDispatcher();

        raisedCount.Should().Be(1);
    }

    [StaFact]
    public void Dispose_AfterInitialize_UnsubscribesFromHotkeyEvents()
    {
        _coordinator.Initialize();

        var raisedCount = 0;
        _coordinator.HotkeyPressed += (_, _) => raisedCount++;

        _coordinator.Dispose();

        _hotkeyService.TryHandleHotkeyMessageForTest().Should().BeTrue();
        FlushDispatcher();

        raisedCount.Should().Be(0);
    }

    [StaFact]
    public void SuspendHotkeyRegistration_BeforeInitialize_DoesNotThrow()
    {
        var act = () => _coordinator.SuspendHotkeyRegistration();

        act.Should().NotThrow();
    }

    [StaFact]
    public void ResumeHotkeyRegistration_BeforeInitialize_DoesNotThrow()
    {
        var act = () => _coordinator.ResumeHotkeyRegistration();

        act.Should().NotThrow();
    }

    [StaFact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var act = () =>
        {
            _coordinator.Dispose();
            _coordinator.Dispose();
        };

        act.Should().NotThrow();
    }

    [StaFact]
    public void Initialize_AfterDispose_ThrowsObjectDisposedException()
    {
        _coordinator.Dispose();

        var act = () => _coordinator.Initialize();

        act.Should().Throw<ObjectDisposedException>();
    }

    private static void FlushDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
