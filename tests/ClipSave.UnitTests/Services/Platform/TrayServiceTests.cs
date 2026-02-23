using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Windows.Forms;

namespace ClipSave.UnitTests;

[UnitTest]
public class TrayServiceTests : IDisposable
{
    private readonly TrayService _trayService;

    public TrayServiceTests()
    {
        var logger = Mock.Of<ILogger<TrayService>>();
        _trayService = new TrayService(logger);
    }

    public void Dispose()
    {
        _trayService.Dispose();
    }

    [StaFact]
    public void Constructor_ShowsTrayIconImmediately()
    {
        _trayService.IsTrayIconVisibleForTest().Should().BeTrue();
    }

    [StaFact]
    public void HandleTrayIconClick_LeftClick_RaisesSettingsRequested()
    {
        var eventRaised = false;
        _trayService.SettingsRequested += (_, _) => eventRaised = true;

        _trayService.HandleTrayIconClick(TrayService.TrayClickButton.Left);

        eventRaised.Should().BeTrue("left click should raise SettingsRequested to open settings");
    }

    [StaFact]
    public void HandleTrayIconClick_RightClick_DoesNotRaiseSettingsRequested()
    {
        var eventRaised = false;
        _trayService.SettingsRequested += (_, _) => eventRaised = true;

        _trayService.HandleTrayIconClick(TrayService.TrayClickButton.Right);

        eventRaised.Should().BeFalse("right click should not raise SettingsRequested because it opens context menu");
    }

    [StaFact]
    public void HandleTrayIconClick_MiddleClick_DoesNotRaiseSettingsRequested()
    {
        var eventRaised = false;
        _trayService.SettingsRequested += (_, _) => eventRaised = true;

        _trayService.HandleTrayIconClick(TrayService.TrayClickButton.Middle);

        eventRaised.Should().BeFalse("middle click should not raise SettingsRequested");
    }

    [StaFact]
    public void StartupSettingsMenu_Click_RaisesStartupSettingsRequested()
    {
        var eventRaised = false;
        _trayService.StartupSettingsRequested += (_, _) => eventRaised = true;

        _trayService.TriggerStartupSettingsMenuForTest();

        eventRaised.Should().BeTrue("startup settings menu should raise StartupSettingsRequested");
    }

    [StaFact]
    public void NotificationSettingsMenu_Click_RaisesNotificationSettingsRequested()
    {
        var eventRaised = false;
        _trayService.NotificationSettingsRequested += (_, _) => eventRaised = true;

        _trayService.TriggerNotificationSettingsMenuForTest();

        eventRaised.Should().BeTrue("notification settings menu should raise NotificationSettingsRequested");
    }

    [StaFact]
    public void TryHandleContextMenuShortcut_MatchingMnemonic_RaisesNotificationSettingsRequested()
    {
        var eventRaised = false;
        _trayService.NotificationSettingsRequested += (_, _) => eventRaised = true;

        var handled = _trayService.TryHandleContextMenuShortcut(Keys.N);

        handled.Should().BeTrue("N is the mnemonic for the notification settings menu");
        eventRaised.Should().BeTrue("notification settings should open when the mnemonic key is pressed");
    }

    [StaFact]
    public void TryHandleContextMenuShortcut_UnknownKey_DoesNothing()
    {
        var notificationRaised = false;
        _trayService.NotificationSettingsRequested += (_, _) => notificationRaised = true;

        var handled = _trayService.TryHandleContextMenuShortcut(Keys.Z);

        handled.Should().BeFalse("there is no tray menu item mapped to Z");
        notificationRaised.Should().BeFalse("unmapped keys should not trigger tray actions");
    }

    [Theory]
    [InlineData(NotificationSeverity.Info, ToolTipIcon.Info)]
    [InlineData(NotificationSeverity.Warning, ToolTipIcon.Warning)]
    [InlineData(NotificationSeverity.Error, ToolTipIcon.Error)]
    public void ResolveToolTipIcon_MapsSeverity(NotificationSeverity severity, ToolTipIcon expected)
    {
        var icon = TrayService.ResolveToolTipIcon(severity);

        icon.Should().Be(expected);
    }
}
