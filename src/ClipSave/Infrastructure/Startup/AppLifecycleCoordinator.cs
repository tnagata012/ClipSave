using ClipSave.Models;
using ClipSave.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Threading;

namespace ClipSave.Infrastructure.Startup;

internal sealed class AppLifecycleCoordinator : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger _logger;
    private readonly Action _shutdownApplication;

    private SingleInstanceService? _singleInstanceService;
    private TrayService? _trayService;
    private SavePipeline? _savePipeline;
    private NotificationService? _notificationService;
    private SettingsService? _settingsService;
    private LocalizationService? _localizationService;
    private AppHotkeyCoordinator? _hotkeyCoordinator;
    private AppWindowCoordinator? _windowCoordinator;
    private int _pendingSecondInstanceLaunchRequested;
    private bool _started;
    private bool _disposed;

    public AppLifecycleCoordinator(
        IServiceProvider serviceProvider,
        Dispatcher dispatcher,
        ILogger logger,
        Action shutdownApplication)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _shutdownApplication = shutdownApplication ?? throw new ArgumentNullException(nameof(shutdownApplication));
    }

    public LocalizationService? LocalizationService => _localizationService;

    public SettingsService? SettingsService => _settingsService;

    internal void SetSettingsServiceForTest(SettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    internal void ShowStartupGuidanceIfNeededForTest()
    {
        ShowStartupGuidanceIfNeeded();
    }

    public bool TryStartPrimaryInstance()
    {
        ThrowIfDisposed();

        if (_started)
        {
            return true;
        }

        _singleInstanceService = _serviceProvider.GetRequiredService<SingleInstanceService>();
        if (!_singleInstanceService.TryAcquireOrNotify())
        {
            return false;
        }

        _singleInstanceService.SecondInstanceLaunched += OnSecondInstanceLaunched;

        InitializeApplicationComponents();
        ShowStartupGuidanceIfNeeded();

        _started = true;
        return true;
    }

    private void InitializeApplicationComponents()
    {
        ResolveCoreServices();
        InitializeCoordinators();
        SubscribeCoreEvents();
        ProcessPendingSettingsCorruption();
        _hotkeyCoordinator!.Initialize();
        TryProcessPendingSecondInstanceLaunch();
    }

    private void ResolveCoreServices()
    {
        _localizationService = _serviceProvider.GetRequiredService<LocalizationService>();
        _settingsService = _serviceProvider.GetRequiredService<SettingsService>();
        _localizationService.SetLanguage(_settingsService.Current.Ui.Language);
        _notificationService = _serviceProvider.GetRequiredService<NotificationService>();
        _trayService = _serviceProvider.GetRequiredService<TrayService>();
        _savePipeline = _serviceProvider.GetRequiredService<SavePipeline>();
    }

    private void InitializeCoordinators()
    {
        var hotkeyService = _serviceProvider.GetRequiredService<HotkeyService>();
        _hotkeyCoordinator = new AppHotkeyCoordinator(
            hotkeyService,
            _settingsService!,
            _notificationService!,
            _logger,
            GetLocalizedString);

        _windowCoordinator = new AppWindowCoordinator(
            _serviceProvider,
            _settingsService!,
            _localizationService!,
            _hotkeyCoordinator,
            _logger);
    }

    private void SubscribeCoreEvents()
    {
        _settingsService!.SettingsChanged += OnSettingsChanged;
        _settingsService.SettingsCorruptionDetected += OnSettingsCorruptionDetected;

        _trayService!.SettingsRequested += OnSettingsRequested;
        _trayService.StartupSettingsRequested += OnStartupSettingsRequested;
        _trayService.NotificationSettingsRequested += OnNotificationSettingsRequested;
        _trayService.AboutRequested += OnAboutRequested;
        _trayService.ExitRequested += OnExitRequested;

        _hotkeyCoordinator!.HotkeyPressed += OnHotkeyPressed;
        _notificationService!.NotificationRequested += OnNotificationRequested;
    }

    private void ProcessPendingSettingsCorruption()
    {
        if (_settingsService!.TryDequeuePendingCorruption(out var pendingCorruption) &&
            pendingCorruption != null)
        {
            OnSettingsCorruptionDetected(_settingsService, pendingCorruption);
        }
    }

    private void UnsubscribeCoreEvents()
    {
        if (_singleInstanceService != null)
        {
            _singleInstanceService.SecondInstanceLaunched -= OnSecondInstanceLaunched;
        }

        if (_settingsService != null)
        {
            _settingsService.SettingsChanged -= OnSettingsChanged;
            _settingsService.SettingsCorruptionDetected -= OnSettingsCorruptionDetected;
        }

        if (_trayService != null)
        {
            _trayService.SettingsRequested -= OnSettingsRequested;
            _trayService.StartupSettingsRequested -= OnStartupSettingsRequested;
            _trayService.NotificationSettingsRequested -= OnNotificationSettingsRequested;
            _trayService.AboutRequested -= OnAboutRequested;
            _trayService.ExitRequested -= OnExitRequested;
        }

        if (_hotkeyCoordinator != null)
        {
            _hotkeyCoordinator.HotkeyPressed -= OnHotkeyPressed;
        }

        if (_notificationService != null)
        {
            _notificationService.NotificationRequested -= OnNotificationRequested;
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        _ = HandleHotkeyPressedAsync();
    }

    private async Task HandleHotkeyPressedAsync()
    {
        if (_savePipeline == null)
        {
            _logger.LogWarning("Hotkey was pressed before SavePipeline was initialized");
            return;
        }

        try
        {
            await _savePipeline.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing hotkey");
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        _windowCoordinator?.ShowOrActivateSettingsDialog();
    }

    private void OnAboutRequested(object? sender, EventArgs e)
    {
        _windowCoordinator?.ShowOrActivateAboutWindow();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _logger.LogInformation("Exit requested");
        _shutdownApplication();
    }

    private void OnStartupSettingsRequested(object? sender, EventArgs e)
    {
        OpenWindowsSettingsPage(
            "ms-settings:startupapps",
            openedLogMessage: "Opened Windows startup settings",
            failedLogMessage: "Failed to open Windows startup settings",
            failedNotificationMessage: GetLocalizedString("App_OpenStartupSettingsFailed"));
    }

    private void OnNotificationSettingsRequested(object? sender, EventArgs e)
    {
        OpenWindowsSettingsPage(
            "ms-settings:notifications",
            openedLogMessage: "Opened Windows notification settings",
            failedLogMessage: "Failed to open Windows notification settings",
            failedNotificationMessage: GetLocalizedString("App_OpenNotificationSettingsFailed"));
    }

    private void OnSecondInstanceLaunched(object? sender, EventArgs e)
    {
        _logger.LogInformation("Detected second instance launch; requesting settings window");
        Interlocked.Exchange(ref _pendingSecondInstanceLaunchRequested, 1);
        _ = _dispatcher.BeginInvoke(TryProcessPendingSecondInstanceLaunch);
    }

    private void TryProcessPendingSecondInstanceLaunch()
    {
        if (Interlocked.Exchange(ref _pendingSecondInstanceLaunchRequested, 0) == 0)
        {
            return;
        }

        if (_windowCoordinator == null)
        {
            _logger.LogDebug("Deferred second-instance settings request until window coordinator initialization completes");
            Interlocked.Exchange(ref _pendingSecondInstanceLaunchRequested, 1);
            return;
        }

        try
        {
            _windowCoordinator.ShowOrActivateSettingsDialog();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open settings window for second-instance request");
        }
    }

    private void OnNotificationRequested(object? sender, NotificationMessage notification)
    {
        _trayService?.ShowBalloonNotification(notification.Message, notification.Severity);
    }

    private void OnSettingsCorruptionDetected(object? sender, SettingsCorruptionEventArgs e)
    {
        _logger.LogWarning("Received settings corruption notification");

        var message = e.BackupPath != null
            ? FormatLocalizedString("App_SettingsCorruptionWithBackup", e.BackupPath)
            : GetLocalizedString("App_SettingsCorruption");

        _notificationService?.NotifyError(message);
    }

    private void ShowStartupGuidanceIfNeeded()
    {
        try
        {
            if (_settingsService?.Current.Advanced.StartupGuidanceShown == true)
            {
                return;
            }

            _trayService?.ShowBalloonNotification(GetLocalizedString("App_StartupGuidance"));
            _settingsService?.UpdateSettings(settings => settings.Advanced.StartupGuidanceShown = true);
            _logger.LogInformation("Displayed first-run startup guidance");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to display first-run startup guidance");
        }
    }

    private void OpenWindowsSettingsPage(
        string settingsUri,
        string openedLogMessage,
        string failedLogMessage,
        string failedNotificationMessage)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = settingsUri,
                UseShellExecute = true
            });
            _logger.LogInformation(openedLogMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, failedLogMessage);
            _notificationService?.NotifyError(failedNotificationMessage);
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        _localizationService?.SetLanguage(settings.Ui.Language);
    }

    private string GetLocalizedString(string key)
    {
        return _localizationService?.GetString(key) ?? key;
    }

    private string FormatLocalizedString(string key, params object[] args)
    {
        if (_localizationService != null)
        {
            return _localizationService.Format(key, args);
        }

        return args.Length == 0
            ? key
            : string.Format(CultureInfo.InvariantCulture, key, args);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnsubscribeCoreEvents();

        _windowCoordinator?.Dispose();
        _windowCoordinator = null;

        _hotkeyCoordinator?.Dispose();
        _hotkeyCoordinator = null;

        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
