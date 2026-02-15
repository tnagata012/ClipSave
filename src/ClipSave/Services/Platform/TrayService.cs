using ClipSave.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WpfApplication = System.Windows.Application;

namespace ClipSave.Services;

public class TrayService : IDisposable
{
    internal enum TrayClickButton
    {
        Left,
        Right,
        Middle,
        Other
    }

    private readonly ILogger<TrayService> _logger;
    private readonly LocalizationService _localizationService;
    private readonly NotifyIcon _notifyIcon;
    private readonly SynchronizationContext? _syncContext;
    private readonly Icon? _appIcon;
    private ContextMenuStrip? _contextMenu;
    private ToolStripMenuItem? _settingsMenuItem;
    private ToolStripMenuItem? _startupSettingsMenuItem;
    private ToolStripMenuItem? _notificationSettingsMenuItem;
    private ToolStripMenuItem? _aboutMenuItem;
    private ToolStripMenuItem? _exitMenuItem;

    public event EventHandler? SettingsRequested;
    public event EventHandler? StartupSettingsRequested;
    public event EventHandler? NotificationSettingsRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? ExitRequested;

    public TrayService(
        ILogger<TrayService> logger)
        : this(logger, new LocalizationService(NullLogger<LocalizationService>.Instance))
    {
    }

    public TrayService(
        ILogger<TrayService> logger,
        LocalizationService localizationService)
    {
        _logger = logger;
        _localizationService = localizationService;
        _syncContext = SynchronizationContext.Current;

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "ClipSave"
        };

        _appIcon = LoadTrayIcon();
        _notifyIcon.Icon = _appIcon ?? SystemIcons.Application;

        _notifyIcon.MouseClick += OnTrayIconClick;
        _localizationService.LanguageChanged += OnLanguageChanged;

        BuildContextMenu();

        _logger.LogInformation("Initialized TrayService");
    }

    private void BuildContextMenu()
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.KeyDown += OnContextMenuKeyDown;

        _settingsMenuItem = new ToolStripMenuItem(_localizationService.GetString("Tray_Menu_Settings"));
        _settingsMenuItem.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(_settingsMenuItem);

        _startupSettingsMenuItem = new ToolStripMenuItem(_localizationService.GetString("Tray_Menu_StartupSettings"));
        _startupSettingsMenuItem.Click += (s, e) => StartupSettingsRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(_startupSettingsMenuItem);

        _notificationSettingsMenuItem = new ToolStripMenuItem(_localizationService.GetString("Tray_Menu_NotificationSettings"));
        _notificationSettingsMenuItem.Click += (s, e) => NotificationSettingsRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(_notificationSettingsMenuItem);

        _aboutMenuItem = new ToolStripMenuItem(_localizationService.GetString("Tray_Menu_About"));
        _aboutMenuItem.Click += (s, e) => AboutRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(_aboutMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        _exitMenuItem = new ToolStripMenuItem(_localizationService.GetString("Tray_Menu_Exit"));
        _exitMenuItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(_exitMenuItem);

        _contextMenu = contextMenu;
        _notifyIcon.ContextMenuStrip = contextMenu;

        _logger.LogDebug("Built tray context menu");
    }

    private void OnTrayIconClick(object? sender, MouseEventArgs e)
    {
        var button = e.Button switch
        {
            MouseButtons.Left => TrayClickButton.Left,
            MouseButtons.Right => TrayClickButton.Right,
            MouseButtons.Middle => TrayClickButton.Middle,
            _ => TrayClickButton.Other
        };

        HandleTrayIconClick(button);
    }

    internal void HandleTrayIconClick(TrayClickButton button)
    {
        if (button == TrayClickButton.Left)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void TriggerExitMenuForTest()
    {
        _exitMenuItem?.PerformClick();
    }

    internal void TriggerStartupSettingsMenuForTest()
    {
        _startupSettingsMenuItem?.PerformClick();
    }

    internal void TriggerNotificationSettingsMenuForTest()
    {
        _notificationSettingsMenuItem?.PerformClick();
    }

    internal void TriggerAboutMenuForTest()
    {
        _aboutMenuItem?.PerformClick();
    }

    internal (string Settings, string StartupSettings, string NotificationSettings, string About, string Exit) GetMenuTextSnapshotForTest()
    {
        return (
            _settingsMenuItem?.Text ?? string.Empty,
            _startupSettingsMenuItem?.Text ?? string.Empty,
            _notificationSettingsMenuItem?.Text ?? string.Empty,
            _aboutMenuItem?.Text ?? string.Empty,
            _exitMenuItem?.Text ?? string.Empty);
    }

    internal bool TryHandleContextMenuShortcut(Keys keyCode)
    {
        return TryInvokeMenuItemByMnemonic(keyCode);
    }

    public void ShowBalloonNotification(string message, NotificationSeverity severity = NotificationSeverity.Info)
    {
        try
        {
            var icon = ResolveToolTipIcon(severity);
            PostToUi(() => _notifyIcon.ShowBalloonTip(3000, "ClipSave", message, icon));

            _logger.LogDebug("Displayed balloon notification: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to display balloon notification");
        }
    }

    internal static ToolTipIcon ResolveToolTipIcon(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Error => ToolTipIcon.Error,
            NotificationSeverity.Warning => ToolTipIcon.Warning,
            _ => ToolTipIcon.Info
        };
    }

    private void PostToUi(Action action)
    {
        if (_syncContext == null)
        {
            action();
            return;
        }

        _syncContext.Post(_ =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception occurred during tray operation");
            }
        }, null);
    }

    public void Dispose()
    {
        _localizationService.LanguageChanged -= OnLanguageChanged;
        if (_contextMenu != null)
        {
            _contextMenu.KeyDown -= OnContextMenuKeyDown;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _appIcon?.Dispose();
        _logger.LogInformation("Disposed TrayService");
        GC.SuppressFinalize(this);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        PostToUi(UpdateLocalizedMenuText);
    }

    private void UpdateLocalizedMenuText()
    {
        if (_settingsMenuItem == null ||
            _startupSettingsMenuItem == null ||
            _notificationSettingsMenuItem == null ||
            _aboutMenuItem == null ||
            _exitMenuItem == null)
        {
            return;
        }

        _settingsMenuItem.Text = _localizationService.GetString("Tray_Menu_Settings");
        _startupSettingsMenuItem.Text = _localizationService.GetString("Tray_Menu_StartupSettings");
        _notificationSettingsMenuItem.Text = _localizationService.GetString("Tray_Menu_NotificationSettings");
        _aboutMenuItem.Text = _localizationService.GetString("Tray_Menu_About");
        _exitMenuItem.Text = _localizationService.GetString("Tray_Menu_Exit");
    }

    private void OnContextMenuKeyDown(object? sender, KeyEventArgs e)
    {
        if (!TryInvokeMenuItemByMnemonic(e.KeyCode))
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private bool TryInvokeMenuItemByMnemonic(Keys keyCode)
    {
        var keyChar = ToShortcutChar(keyCode);
        if (keyChar == null || _contextMenu == null)
        {
            return false;
        }

        foreach (var item in _contextMenu.Items.OfType<ToolStripMenuItem>())
        {
            if (item.Enabled != true)
            {
                continue;
            }

            var mnemonic = GetMnemonicChar(item.Text);
            if (mnemonic == null || mnemonic.Value != keyChar.Value)
            {
                continue;
            }

            item.PerformClick();
            return true;
        }

        return false;
    }

    private static char? ToShortcutChar(Keys keyCode)
    {
        if (keyCode >= Keys.A && keyCode <= Keys.Z)
        {
            return char.ToUpperInvariant((char)('A' + (keyCode - Keys.A)));
        }

        if (keyCode >= Keys.D0 && keyCode <= Keys.D9)
        {
            return (char)('0' + (keyCode - Keys.D0));
        }

        if (keyCode >= Keys.NumPad0 && keyCode <= Keys.NumPad9)
        {
            return (char)('0' + (keyCode - Keys.NumPad0));
        }

        return null;
    }

    private static char? GetMnemonicChar(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] != '&')
            {
                continue;
            }

            if (text[i + 1] == '&')
            {
                i++;
                continue;
            }

            return char.ToUpperInvariant(text[i + 1]);
        }

        return null;
    }

    private static Icon? LoadTrayIcon()
    {
        try
        {
            var resourceInfo = WpfApplication.GetResourceStream(
                new Uri("pack://application:,,,/Assets/ClipSave.ico"));
            if (resourceInfo?.Stream == null)
            {
                return null;
            }

            using var stream = resourceInfo.Stream;
            using var icon = new Icon(stream);
            return (Icon)icon.Clone();
        }
        catch
        {
            return null;
        }
    }
}
