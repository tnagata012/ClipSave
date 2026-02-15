using ClipSave.Infrastructure;
using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class NotificationServiceIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localizationService;
    private readonly NotificationService _notificationService;

    public NotificationServiceIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_Notification_Integration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        _settingsService = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _testDirectory);
        _settingsService.UpdateSettings(settings => settings.Ui.Language = AppLanguage.English);

        _localizationService = new LocalizationService(_loggerFactory.CreateLogger<LocalizationService>());
        _localizationService.SetLanguage(AppLanguage.English);

        _notificationService = new NotificationService(
            _loggerFactory.CreateLogger<NotificationService>(),
            _settingsService,
            _localizationService);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    [Spec("SPEC-050-003")]
    public void NotifySuccess_WhenEnabled_RaisesInfoNotification()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.Notification.OnSuccess = true;
            settings.Notification.OnNoContent = false;
            settings.Notification.OnError = false;
        });

        NotificationMessage? notification = null;
        _notificationService.NotificationRequested += (_, message) => notification = message;

        _notificationService.NotifySuccess(@"C:\Temp\sample.txt");

        notification.Should().NotBeNull();
        notification!.Severity.Should().Be(NotificationSeverity.Info);
        notification.Message.Should().Be(_localizationService.Format("Notification_SaveCompleted", "sample.txt"));
    }

    [Fact]
    [Spec("SPEC-000-005")]
    [Spec("SPEC-050-004")]
    public void NotifyNoContent_WhenEnabled_RaisesInfoNotification()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.Notification.OnSuccess = false;
            settings.Notification.OnNoContent = true;
            settings.Notification.OnError = false;
        });

        NotificationMessage? notification = null;
        _notificationService.NotificationRequested += (_, message) => notification = message;

        _notificationService.NotifyNoContent();

        notification.Should().NotBeNull();
        notification!.Severity.Should().Be(NotificationSeverity.Info);
        notification.Message.Should().Be(_localizationService.GetString("Notification_NoContent"));
    }

    [Fact]
    [Spec("SPEC-030-001")]
    [Spec("SPEC-030-002")]
    [Spec("SPEC-050-005")]
    [Spec("SPEC-050-008")]
    public void NotifyError_WhenEnabled_RaisesErrorNotification()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.Notification.OnSuccess = false;
            settings.Notification.OnNoContent = false;
            settings.Notification.OnError = true;
        });

        NotificationMessage? notification = null;
        _notificationService.NotificationRequested += (_, message) => notification = message;

        _notificationService.NotifyError("failed");

        notification.Should().NotBeNull();
        notification!.Severity.Should().Be(NotificationSeverity.Error);
        notification.Message.Should().Be(_localizationService.Format("Notification_ErrorPrefix", "failed"));
    }

    [Fact]
    [Spec("SPEC-050-007")]
    public void NotifyAllDisabled_DoesNotRaiseNotification()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.Notification.OnSuccess = false;
            settings.Notification.OnNoContent = false;
            settings.Notification.OnError = false;
        });

        var raised = false;
        _notificationService.NotificationRequested += (_, _) => raised = true;

        _notificationService.NotifySuccess(@"C:\Temp\sample.txt");
        _notificationService.NotifyNoContent();
        _notificationService.NotifyError("failed");

        raised.Should().BeFalse();
    }

    [Fact]
    [Spec("SPEC-000-004")]
    [Spec("SPEC-020-006")]
    [Spec("SPEC-050-006")]
    public void NotifyResult_ForQuietResultKinds_DoesNotRaiseNotification()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.Notification.OnSuccess = true;
            settings.Notification.OnNoContent = true;
            settings.Notification.OnError = true;
        });

        var raised = false;
        _notificationService.NotificationRequested += (_, _) => raised = true;

        _notificationService.NotifyResult(SaveResult.CreateUnsupportedWindow());
        _notificationService.NotifyResult(SaveResult.CreateBusy());
        _notificationService.NotifyResult(SaveResult.CreateContentTypeDisabled(ContentType.Text));

        raised.Should().BeFalse();
    }

    [Fact]
    [Spec("SPEC-090-005")]
    public void NotifyNoContent_UsesCurrentUiLanguage()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.Ui.Language = AppLanguage.Japanese;
            settings.Notification.OnNoContent = true;
            settings.Notification.OnSuccess = false;
            settings.Notification.OnError = false;
        });
        _localizationService.SetLanguage(AppLanguage.Japanese);

        NotificationMessage? notification = null;
        _notificationService.NotificationRequested += (_, message) => notification = message;

        _notificationService.NotifyNoContent();

        notification.Should().NotBeNull();
        notification!.Message.Should().Be(_localizationService.GetString("Notification_NoContent"));
    }
}
