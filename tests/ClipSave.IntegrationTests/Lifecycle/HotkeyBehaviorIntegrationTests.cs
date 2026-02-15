using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;
using System.Windows.Threading;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class HotkeyBehaviorIntegrationTests
{
    [Fact]
    [Spec("SPEC-020-002")]
    public void AppHotkeyCoordinator_ConflictPath_RestoresPreviousHotkeyContract()
    {
        var source = ReadSource("src", "ClipSave", "Infrastructure", "Startup", "AppHotkeyCoordinator.cs");

        source.Should().Contain("RestorePreviousHotkey();");
        source.Should().Contain("_settingsService.UpdateSettings(settings => settings.Hotkey = CloneHotkeySettings(fallback));");
        source.Should().Contain("var restored = _hotkeyService.Register(fallback.Modifiers, fallback.Key);");
    }

    [StaFact]
    [Spec("SPEC-020-003")]
    public void HotkeyMessage_RepeatWithinSuppressionWindow_IsIgnored()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        using var hotkeyService = new HotkeyService(loggerFactory.CreateLogger<HotkeyService>());
        var pressedCount = 0;
        hotkeyService.HotkeyPressed += (_, _) => pressedCount++;

        InvokeHotkeyMessage(hotkeyService);
        FlushDispatcher();
        pressedCount.Should().Be(1);

        InvokeHotkeyMessage(hotkeyService);
        FlushDispatcher();
        pressedCount.Should().Be(1);
    }

    private static void InvokeHotkeyMessage(HotkeyService service)
    {
        var wndProc = typeof(HotkeyService).GetMethod(
            "WndProc",
            BindingFlags.Instance | BindingFlags.NonPublic);
        wndProc.Should().NotBeNull();

        var args = new object?[]
        {
            IntPtr.Zero,
            0x0312,
            new IntPtr(1),
            IntPtr.Zero,
            false
        };

        _ = wndProc!.Invoke(service, args);
        ((bool)args[4]!).Should().BeTrue();
    }

    private static void FlushDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static string ReadSource(params string[] pathSegments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var parts = new string[pathSegments.Length + 1];
        parts[0] = root;
        Array.Copy(pathSegments, 0, parts, 1, pathSegments.Length);
        return File.ReadAllText(Path.Combine(parts));
    }
}
