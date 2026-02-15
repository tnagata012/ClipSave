using ClipSave.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;

namespace ClipSave.Services;

public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localizationService;

    public event EventHandler<NotificationMessage>? NotificationRequested;

    public NotificationService(
        ILogger<NotificationService> logger,
        SettingsService settingsService)
        : this(logger, settingsService, new LocalizationService(NullLogger<LocalizationService>.Instance))
    {
    }

    public NotificationService(
        ILogger<NotificationService> logger,
        SettingsService settingsService,
        LocalizationService localizationService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _localizationService = localizationService;
        _localizationService.SetLanguage(settingsService.Current.Ui.Language);
    }

    public void NotifySuccess(string filePath)
    {
        var settings = _settingsService.Current.Notification;

        if (settings.OnSuccess)
        {
            var fileName = Path.GetFileName(filePath);
            NotificationRequested?.Invoke(
                this,
                new NotificationMessage(_localizationService.Format("Notification_SaveCompleted", fileName),
                    NotificationSeverity.Info));
        }

        _logger.LogDebug("Handled save success notification: {FilePath}", filePath);
    }

    public void NotifyNoContent()
    {
        var settings = _settingsService.Current.Notification;
        if (settings.OnNoContent)
        {
            NotificationRequested?.Invoke(
                this,
                new NotificationMessage(_localizationService.GetString("Notification_NoContent"),
                    NotificationSeverity.Info));
        }

        _logger.LogDebug("No saveable clipboard content was found");
    }

    public void NotifyError(string message)
    {
        var settings = _settingsService.Current.Notification;

        if (settings.OnError)
        {
            NotificationRequested?.Invoke(
                this,
                new NotificationMessage(_localizationService.Format("Notification_ErrorPrefix", message),
                    NotificationSeverity.Error));
        }

        _logger.LogDebug("Handled error notification: {Message}", message);
    }

    public void NotifyResult(SaveResult result)
    {
        switch (result.Kind)
        {
            case SaveResultKind.Success:
                if (result.FilePath != null)
                {
                    NotifySuccess(result.FilePath);
                }
                else
                {
                    _logger.LogWarning("Success result did not contain a file path");
                    NotifyError(_localizationService.GetString("Notification_MissingFilePath"));
                }
                break;
            case SaveResultKind.NoContent:
                NotifyNoContent();
                break;
            case SaveResultKind.Busy:
            case SaveResultKind.UnsupportedWindow:
            case SaveResultKind.ContentTypeDisabled:
                _logger.LogDebug("Received non-notifiable result: {Kind}", result.Kind);
                break;
            case SaveResultKind.Error:
            default:
                NotifyError(result.ErrorMessage ?? _localizationService.GetString("Notification_UnknownError"));
                break;
        }
    }
}
