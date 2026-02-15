using ClipSave.Services;
using ClipSave.ViewModels.About;
using ClipSave.ViewModels.Settings;
using ClipSave.Views.About;
using ClipSave.Views.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipSave.Infrastructure.Startup;

internal sealed class AppWindowCoordinator : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localizationService;
    private readonly AppHotkeyCoordinator _hotkeyCoordinator;
    private readonly ILogger _logger;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private bool _isShuttingDown;
    private bool _disposed;

    public AppWindowCoordinator(
        IServiceProvider serviceProvider,
        SettingsService settingsService,
        LocalizationService localizationService,
        AppHotkeyCoordinator hotkeyCoordinator,
        ILogger logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _hotkeyCoordinator = hotkeyCoordinator ?? throw new ArgumentNullException(nameof(hotkeyCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void ShowOrActivateSettingsDialog()
    {
        ThrowIfDisposed();
        _logger.LogDebug("Settings window display requested");

        _localizationService.SetLanguage(_settingsService.Current.Ui.Language);

        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        var hotkeySuspended = false;
        try
        {
            var viewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
            var window = new SettingsWindow
            {
                DataContext = viewModel
            };
            window.Closed += OnSettingsWindowClosed;

            _hotkeyCoordinator.SuspendHotkeyRegistration();
            hotkeySuspended = true;

            _settingsWindow = window;
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show settings window");

            if (_settingsWindow != null)
            {
                _settingsWindow.Closed -= OnSettingsWindowClosed;
                _settingsWindow = null;

                if (hotkeySuspended && !_isShuttingDown)
                {
                    _hotkeyCoordinator.ResumeHotkeyRegistration();
                }
            }

            throw;
        }
    }

    public void ShowOrActivateAboutWindow()
    {
        ThrowIfDisposed();
        _logger.LogDebug("About window display requested");

        if (_aboutWindow != null)
        {
            _aboutWindow.Activate();
            return;
        }

        var viewModel = _serviceProvider.GetRequiredService<AboutViewModel>();
        var window = new AboutWindow
        {
            DataContext = viewModel
        };
        window.Closed += OnAboutWindowClosed;

        _aboutWindow = window;
        window.Show();
    }

    private void OnSettingsWindowClosed(object? sender, EventArgs e)
    {
        if (sender is SettingsWindow window)
        {
            window.Closed -= OnSettingsWindowClosed;
        }

        _settingsWindow = null;

        if (!_isShuttingDown)
        {
            _hotkeyCoordinator.ResumeHotkeyRegistration();
        }
    }

    private void OnAboutWindowClosed(object? sender, EventArgs e)
    {
        if (sender is AboutWindow window)
        {
            window.Closed -= OnAboutWindowClosed;
        }

        _aboutWindow = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _isShuttingDown = true;

        if (_settingsWindow != null)
        {
            _settingsWindow.Closed -= OnSettingsWindowClosed;
            _settingsWindow.Close();
            _settingsWindow = null;
        }

        if (_aboutWindow != null)
        {
            _aboutWindow.Closed -= OnAboutWindowClosed;
            _aboutWindow.Close();
            _aboutWindow = null;
        }

        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
