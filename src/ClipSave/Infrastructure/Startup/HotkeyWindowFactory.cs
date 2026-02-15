using System.Windows;

namespace ClipSave.Infrastructure.Startup;

internal static class HotkeyWindowFactory
{
    public static Window CreateHiddenWindow()
    {
        var window = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false
        };

        window.Show();
        window.Hide();

        return window;
    }
}
