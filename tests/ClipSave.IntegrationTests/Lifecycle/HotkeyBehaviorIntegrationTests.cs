using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
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
        var tick = 1_000L;
        using var hotkeyService = new HotkeyService(NullLogger<HotkeyService>.Instance, () => tick);
        var pressedCount = 0;
        hotkeyService.HotkeyPressed += (_, _) => pressedCount++;

        // Keep the second trigger inside the suppression window deterministically.
        hotkeyService.TryHandleHotkeyMessageForTest().Should().BeTrue();
        tick += 50;
        hotkeyService.TryHandleHotkeyMessageForTest().Should().BeTrue();
        FlushDispatcher();
        pressedCount.Should().Be(1);
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
