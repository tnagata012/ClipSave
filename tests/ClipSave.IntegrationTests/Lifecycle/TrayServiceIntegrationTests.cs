using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Windows.Forms;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class TrayServiceIntegrationTests
{
    [StaFact]
    [Spec("SPEC-021-001")]
    public void TrayService_Initialized_TrayIconIsVisible()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        using var trayService = new TrayService(loggerFactory.CreateLogger<TrayService>());

        trayService.IsTrayIconVisibleForTest().Should().BeTrue();
    }

    [StaFact]
    [Spec("SPEC-021-003")]
    public void HandleTrayIconClick_LeftClick_RaisesSettingsRequested()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        using var trayService = new TrayService(loggerFactory.CreateLogger<TrayService>());
        var eventRaised = false;

        trayService.SettingsRequested += (_, _) => eventRaised = true;

        trayService.HandleTrayIconClick(TrayService.TrayClickButton.Left);

        eventRaised.Should().BeTrue("left click should raise SettingsRequested to open settings");
    }

    [StaFact]
    [Spec("SPEC-021-004")]
    public void TriggerStartupSettingsMenu_Invoked_RaisesStartupSettingsRequested()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        using var trayService = new TrayService(loggerFactory.CreateLogger<TrayService>());
        var eventRaised = false;

        trayService.StartupSettingsRequested += (_, _) => eventRaised = true;

        trayService.TriggerStartupSettingsMenuForTest();

        eventRaised.Should().BeTrue("startup settings menu should raise StartupSettingsRequested");
    }

    [StaFact]
    [Spec("SPEC-021-005")]
    public void TriggerNotificationSettingsMenu_Invoked_RaisesNotificationSettingsRequested()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        using var trayService = new TrayService(loggerFactory.CreateLogger<TrayService>());
        var eventRaised = false;

        trayService.NotificationSettingsRequested += (_, _) => eventRaised = true;

        trayService.TriggerNotificationSettingsMenuForTest();

        eventRaised.Should().BeTrue("notification settings menu should raise NotificationSettingsRequested");
    }

    [StaFact]
    [Spec("SPEC-021-008")]
    public void TryHandleContextMenuShortcut_MappedKey_RaisesMappedMenuAction()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        using var trayService = new TrayService(loggerFactory.CreateLogger<TrayService>());
        var eventRaised = false;

        trayService.NotificationSettingsRequested += (_, _) => eventRaised = true;

        var handled = trayService.TryHandleContextMenuShortcut(Keys.N);

        handled.Should().BeTrue("N should be recognized as a tray menu access key");
        eventRaised.Should().BeTrue("access key input should trigger the mapped tray menu item");
    }

    [StaFact]
    [Spec("SPEC-050-001")]
    public void ShowBalloonNotification_Invoked_DoesNotThrow()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        using var trayService = new TrayService(loggerFactory.CreateLogger<TrayService>());

        var act = () => trayService.ShowBalloonNotification("test", Models.NotificationSeverity.Info);

        act.Should().NotThrow();
    }
}

