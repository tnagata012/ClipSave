using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Windows.Threading;

namespace ClipSave.UnitTests;

[Collection("HotkeyTests")]
[UnitTest]
public class HotkeyServiceTests
{
    private readonly Mock<ILogger<HotkeyService>> _mockLogger = new();

    [Fact]
    public void HotkeyService_InvalidModifiers_ReturnsFalse()
    {
        using var hotkeyService = new HotkeyService(_mockLogger.Object);

        var result = hotkeyService.Register(new List<string>(), "V");

        result.Should().BeFalse();
    }

    [Fact]
    public void HotkeyService_EmptyKey_ReturnsFalse()
    {
        using var hotkeyService = new HotkeyService(_mockLogger.Object);

        var result = hotkeyService.Register(new List<string> { "Control" }, "");

        result.Should().BeFalse();
    }

    [Fact]
    public void HotkeyService_Unregister_BeforeRegister_DoesNotThrow()
    {
        using var hotkeyService = new HotkeyService(_mockLogger.Object);

        var act = () => hotkeyService.Unregister();

        act.Should().NotThrow();
    }

    [Fact]
    public void HotkeyService_Dispose_CanBeCalledMultipleTimes()
    {
        var hotkeyService = new HotkeyService(_mockLogger.Object);

        var act = () =>
        {
            hotkeyService.Dispose();
            hotkeyService.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void HotkeyService_CanBeConstructed()
    {
        using var hotkeyService = new HotkeyService(_mockLogger.Object);

        hotkeyService.Should().NotBeNull();
    }

    [StaFact]
    public void WndProc_ConsecutiveHotkeysWithinSuppressionWindow_RaisesSingleEvent()
    {
        var tick = 10_000L;
        using var hotkeyService = new HotkeyService(_mockLogger.Object, () => tick);
        var pressedCount = 0;
        hotkeyService.HotkeyPressed += (_, _) => pressedCount++;

        InvokeHotkeyMessage(hotkeyService);
        FlushDispatcher();
        pressedCount.Should().Be(1);

        tick += 50;
        InvokeHotkeyMessage(hotkeyService);
        FlushDispatcher();
        pressedCount.Should().Be(1);
    }

    private static void InvokeHotkeyMessage(HotkeyService service)
    {
        var wndProc = typeof(HotkeyService).GetMethod(
            "WndProc",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        wndProc.Should().NotBeNull();

        var handled = false;
        var args = new object?[]
        {
            IntPtr.Zero,
            0x0312,
            new IntPtr(1),
            IntPtr.Zero,
            handled
        };

        _ = wndProc!.Invoke(service, args);
        args[4].Should().BeOfType<bool>();
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
}
