using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ClipSave.Services;

public class HotkeyService : IDisposable
{
    private readonly ILogger<HotkeyService> _logger;
    private readonly Func<long> _tickProvider;
    private IntPtr _windowHandle;
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 1;
    private bool _isRegistered = false;
    private long _lastTriggerTick;
    private const int RepeatSuppressionMs = 100;

    public event EventHandler? HotkeyPressed;

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private enum ModifierKeys : uint
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }

    public HotkeyService(ILogger<HotkeyService> logger)
        : this(logger, static () => Environment.TickCount64)
    {
    }

    internal HotkeyService(ILogger<HotkeyService> logger, Func<long> tickProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tickProvider = tickProvider ?? throw new ArgumentNullException(nameof(tickProvider));
        _lastTriggerTick = -RepeatSuppressionMs;
    }

    public void Initialize(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;

        var source = HwndSource.FromHwnd(_windowHandle);
        if (source != null)
        {
            source.AddHook(WndProc);
            _logger.LogInformation("Initialized HotkeyService");
        }
        else
        {
            _logger.LogError("Failed to get HwndSource");
        }
    }

    public bool Register(List<string> modifierNames, string keyName)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            _logger.LogError("HotkeyService is not initialized");
            return false;
        }

        if (modifierNames == null || modifierNames.Count == 0)
        {
            _logger.LogWarning("No modifier keys were specified");
            return false;
        }

        if (string.IsNullOrWhiteSpace(keyName))
        {
            _logger.LogWarning("Hotkey key is empty");
            return false;
        }

        Unregister();

        uint modifiers = 0;
        foreach (var modifierName in modifierNames)
        {
            var trimmed = modifierName?.Trim() ?? string.Empty;
            switch (trimmed.ToLowerInvariant())
            {
                case "control":
                case "ctrl":
                    modifiers |= (uint)ModifierKeys.Control;
                    break;
                case "shift":
                    modifiers |= (uint)ModifierKeys.Shift;
                    break;
                case "alt":
                    modifiers |= (uint)ModifierKeys.Alt;
                    break;
                default:
                    _logger.LogWarning("Unknown modifier key: {Modifier}", modifierName);
                    return false;
            }
        }

        if (!Enum.TryParse<Key>(keyName, ignoreCase: true, out var key))
        {
            _logger.LogWarning("Unknown key: {Key}", keyName);
            return false;
        }

        var vk = KeyInterop.VirtualKeyFromKey(key);

        _isRegistered = RegisterHotKey(_windowHandle, HotkeyId, modifiers, (uint)vk);

        if (_isRegistered)
        {
            var modifierStr = string.Join("+", modifierNames);
            _logger.LogInformation("Registered hotkey: {Modifiers}+{Key}",
                modifierStr, keyName);
        }
        else
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogWarning("Failed to register hotkey (Error: {Error})", error);
        }

        return _isRegistered;
    }

    public void Unregister()
    {
        if (_isRegistered && _windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HotkeyId);
            _isRegistered = false;
            _logger.LogInformation("Unregistered hotkey");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            var now = _tickProvider();
            if (now - _lastTriggerTick < RepeatSuppressionMs)
            {
                _logger.LogDebug("Suppressed hotkey repeat input");
                handled = true;
                return IntPtr.Zero;
            }

            _lastTriggerTick = now;
            _logger.LogDebug("Received hotkey input");

            Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                HotkeyPressed?.Invoke(this, EventArgs.Empty));
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();

        if (_windowHandle != IntPtr.Zero)
        {
            var source = HwndSource.FromHwnd(_windowHandle);
            source?.RemoveHook(WndProc);
        }

        GC.SuppressFinalize(this);
    }
}
