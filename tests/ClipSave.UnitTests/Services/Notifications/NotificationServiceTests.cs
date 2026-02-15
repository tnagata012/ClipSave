using ClipSave.Infrastructure;
using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;

namespace ClipSave.UnitTests;

[UnitTest]
public class NotificationServiceTests : IDisposable
{
    private readonly string _testAppDataPath;
    private readonly SettingsService _settingsService;
    private readonly NotificationService _notificationService;

    public NotificationServiceTests()
    {
        _testAppDataPath = Path.Combine(Path.GetTempPath(), $"ClipSave_NotifyTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testAppDataPath);

        var settingsLogger = Mock.Of<ILogger<SettingsService>>();
        var notificationLogger = Mock.Of<ILogger<NotificationService>>();
        var localizationService = new LocalizationService();
        localizationService.SetLanguage(AppLanguage.English);

        _settingsService = new SettingsService(settingsLogger, _testAppDataPath);
        _settingsService.UpdateSettings(settings => settings.Ui.Language = AppLanguage.English);
        _notificationService = new NotificationService(notificationLogger, _settingsService, localizationService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testAppDataPath))
        {
            try
            {
                Directory.Delete(_testAppDataPath, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void NotifySuccess_RespectsSettings()
    {
        _settingsService.UpdateSettings(s =>
        {
            s.Notification.OnSuccess = true;
            s.Notification.OnNoContent = false;
            s.Notification.OnError = false;
        });

        NotificationMessage? notification = null;
        _notificationService.NotificationRequested += (_, msg) => notification = msg;

        _notificationService.NotifySuccess("C:\\temp\\file.png");

        notification.Should().NotBeNull();
        notification!.Message.Should().Be("Saved: file.png");
        notification.Severity.Should().Be(NotificationSeverity.Info);
    }

    [Fact]
    public void NotifyNoContent_RespectsOnNoContent()
    {
        _settingsService.UpdateSettings(s =>
        {
            s.Notification.OnSuccess = false;
            s.Notification.OnNoContent = true;
            s.Notification.OnError = false;
        });

        NotificationMessage? notification = null;
        _notificationService.NotificationRequested += (_, msg) => notification = msg;

        _notificationService.NotifyNoContent();

        notification.Should().NotBeNull();
        notification!.Message.Should().Be("No saveable clipboard content found.");
        notification.Severity.Should().Be(NotificationSeverity.Info);
    }

    [Fact]
    public void NotifyError_RespectsOnError()
    {
        _settingsService.UpdateSettings(s =>
        {
            s.Notification.OnSuccess = false;
            s.Notification.OnNoContent = false;
            s.Notification.OnError = true;
        });

        NotificationMessage? notification = null;
        _notificationService.NotificationRequested += (_, msg) => notification = msg;

        _notificationService.NotifyError("Failed");

        notification.Should().NotBeNull();
        notification!.Message.Should().Be("Error: Failed");
        notification.Severity.Should().Be(NotificationSeverity.Error);
    }

    [Fact]
    public void NotifyAllTypesDisabled_SuppressesAllNotifications()
    {
        _settingsService.UpdateSettings(s =>
        {
            s.Notification.OnSuccess = false;
            s.Notification.OnNoContent = false;
            s.Notification.OnError = false;
        });

        var notified = false;
        _notificationService.NotificationRequested += (_, _) => notified = true;

        _notificationService.NotifySuccess("C:\\temp\\file.png");
        _notificationService.NotifyNoContent();
        _notificationService.NotifyError("Failed");

        notified.Should().BeFalse();
    }

    [Fact]
    public void NotifyResult_Busy_DoesNotRaiseNotification()
    {
        _settingsService.UpdateSettings(s =>
        {
            s.Notification.OnSuccess = true;
            s.Notification.OnNoContent = true;
            s.Notification.OnError = true;
        });

        var notified = false;
        _notificationService.NotificationRequested += (_, _) => notified = true;

        _notificationService.NotifyResult(SaveResult.CreateBusy());

        notified.Should().BeFalse();
    }

    [Fact]
    public void NotifyResult_Error_RaisesErrorSeverity()
    {
        _settingsService.UpdateSettings(s =>
        {
            s.Notification.OnSuccess = false;
            s.Notification.OnNoContent = false;
            s.Notification.OnError = true;
        });

        NotificationMessage? notification = null;
        _notificationService.NotificationRequested += (_, msg) => notification = msg;

        _notificationService.NotifyResult(SaveResult.CreateFailure("Failed"));

        notification.Should().NotBeNull();
        notification!.Message.Should().Be("Error: Failed");
        notification.Severity.Should().Be(NotificationSeverity.Error);
    }

    [Fact]
    public void NotifyResult_SuccessWithoutFilePath_RaisesErrorNotification()
    {
        _settingsService.UpdateSettings(s =>
        {
            s.Notification.OnSuccess = true;
            s.Notification.OnNoContent = false;
            s.Notification.OnError = true;
        });

        NotificationMessage? notification = null;
        _notificationService.NotificationRequested += (_, msg) => notification = msg;

        _notificationService.NotifyResult(new SaveResult { Kind = SaveResultKind.Success });

        notification.Should().NotBeNull();
        notification!.Severity.Should().Be(NotificationSeverity.Error);
        notification.Message.Should().StartWith("Error:");
    }

    [Fact]
    public void NotifyResult_UnsupportedWindow_DoesNotRaiseNotification()
    {
        _settingsService.UpdateSettings(s =>
        {
            s.Notification.OnSuccess = true;
            s.Notification.OnNoContent = true;
            s.Notification.OnError = true;
        });

        var notified = false;
        _notificationService.NotificationRequested += (_, _) => notified = true;

        _notificationService.NotifyResult(SaveResult.CreateUnsupportedWindow());

        notified.Should().BeFalse();
    }
}

