using ClipSave.Models;
using ClipSave.Services;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Interop;

namespace ClipSave.Infrastructure.Startup;

internal sealed class AppHotkeyCoordinator : IDisposable
{
    private readonly HotkeyService _hotkeyService;
    private readonly SettingsService _settingsService;
    private readonly NotificationService _notificationService;
    private readonly ILogger _logger;
    private readonly Func<string, string> _getLocalizedString;
    private Window? _hotkeyWindow;
    private HotkeySettings? _lastHotkeySettings;
    private bool _isShuttingDown;
    private bool _initialized;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;

    public AppHotkeyCoordinator(
        HotkeyService hotkeyService,
        SettingsService settingsService,
        NotificationService notificationService,
        ILogger logger,
        Func<string, string> getLocalizedString)
    {
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _getLocalizedString = getLocalizedString ?? throw new ArgumentNullException(nameof(getLocalizedString));
    }

    public void Initialize()
    {
        ThrowIfDisposed();

        if (_initialized)
        {
            return;
        }

        _hotkeyWindow = HotkeyWindowFactory.CreateHiddenWindow();
        var windowHandle = new WindowInteropHelper(_hotkeyWindow).Handle;

        _hotkeyService.Initialize(windowHandle);
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        RegisterHotkeyFromSettings();

        _initialized = true;
    }

    public void SuspendHotkeyRegistration()
    {
        if (!_initialized || _disposed)
        {
            return;
        }

        _hotkeyService.Unregister();
    }

    public void ResumeHotkeyRegistration()
    {
        if (!_initialized || _disposed)
        {
            return;
        }

        RegisterHotkeyFromSettings();
    }

    private void RegisterHotkeyFromSettings()
    {
        if (_isShuttingDown)
        {
            return;
        }

        var settings = _settingsService.Current.Hotkey;
        var modifiers = settings.Modifiers ?? new List<string>();
        var modifierText = string.Join("+", modifiers);
        var success = _hotkeyService.Register(modifiers, settings.Key);
        if (success)
        {
            _lastHotkeySettings = CloneHotkeySettings(settings);
            return;
        }

        _logger.LogWarning(
            "Failed to register hotkey {Modifiers}+{Key}. It may be in use by another application.",
            modifierText,
            settings.Key);
        RestorePreviousHotkey();
    }

    private void RestorePreviousHotkey()
    {
        if (_lastHotkeySettings == null)
        {
            _logger.LogWarning("Failed to restore previous hotkey because no fallback settings are available");
            _notificationService.NotifyError(_getLocalizedString("App_HotkeyRegisterFailed"));
            return;
        }

        var fallback = CloneHotkeySettings(_lastHotkeySettings);
        try
        {
            _settingsService.UpdateSettings(settings => settings.Hotkey = CloneHotkeySettings(fallback));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist fallback hotkey settings");
        }

        var restored = _hotkeyService.Register(fallback.Modifiers, fallback.Key);
        if (restored)
        {
            _lastHotkeySettings = CloneHotkeySettings(fallback);
            _notificationService.NotifyError(_getLocalizedString("App_HotkeyRegisterFailedRestored"));
            return;
        }

        _logger.LogError("Failed to restore previous hotkey after registration failure");
        _notificationService.NotifyError(_getLocalizedString("App_HotkeyRegisterFailedRestoreFailed"));
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private static HotkeySettings CloneHotkeySettings(HotkeySettings settings)
    {
        return new HotkeySettings
        {
            Key = settings.Key,
            Modifiers = settings.Modifiers?.ToList() ?? new List<string>()
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _isShuttingDown = true;

        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.Unregister();

        _hotkeyWindow?.Close();
        _hotkeyWindow = null;

        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
