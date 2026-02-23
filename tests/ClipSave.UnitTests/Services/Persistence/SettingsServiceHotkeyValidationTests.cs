using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;

namespace ClipSave.UnitTests;

[UnitTest]
public class SettingsServiceHotkeyValidationTests : IDisposable
{
    private readonly string _settingsDirectory;
    private readonly SettingsService _settingsService;

    public SettingsServiceHotkeyValidationTests()
    {
        _settingsDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ClipSave_SettingsServiceHotkeyValidationTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_settingsDirectory);

        _settingsService = new SettingsService(
            Mock.Of<ILogger<SettingsService>>(),
            _settingsDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_settingsDirectory))
        {
            try
            {
                Directory.Delete(_settingsDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void HotkeyChange_UpdatesSettingsAndRaisesSettingsChanged()
    {
        var settingsChangedFired = false;
        _settingsService.SettingsChanged += (_, _) => settingsChangedFired = true;

        _settingsService.UpdateSettings(settings =>
        {
            settings.Hotkey.Modifiers = new List<string> { "Control", "Alt" };
            settings.Hotkey.Key = "S";
        });

        _settingsService.Current.Hotkey.Modifiers.Should().Equal(new[] { "Control", "Alt" });
        _settingsService.Current.Hotkey.Key.Should().Be("S");
        settingsChangedFired.Should().BeTrue();
    }

    [Fact]
    public void InvalidHotkeyUpdate_KeepsPreviousHotkeySettings()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.Hotkey.Modifiers = new List<string> { "Control", "Shift" };
            settings.Hotkey.Key = "V";
        });

        var previousModifiers = _settingsService.Current.Hotkey.Modifiers.ToList();
        var previousKey = _settingsService.Current.Hotkey.Key;

        var act = () => _settingsService.UpdateSettings(settings =>
        {
            settings.Hotkey.Modifiers = new List<string>();
            settings.Hotkey.Key = "A";
        });

        act.Should().Throw<ArgumentException>();
        _settingsService.Current.Hotkey.Modifiers.Should().Equal(previousModifiers);
        _settingsService.Current.Hotkey.Key.Should().Be(previousKey);
    }

    [Fact]
    public void HotkeyModifiers_ValidCombinations_AreAccepted()
    {
        var validCombinations = new[]
        {
            new[] { "Control" },
            new[] { "Shift" },
            new[] { "Alt" },
            new[] { "Control", "Shift" },
            new[] { "Control", "Alt" },
            new[] { "Shift", "Alt" },
            new[] { "Control", "Shift", "Alt" },
            new[] { "Ctrl" },
            new[] { "Ctrl", "Shift" }
        };

        foreach (var combination in validCombinations)
        {
            var act = () => _settingsService.UpdateSettings(settings =>
            {
                settings.Hotkey.Modifiers = combination.ToList();
                settings.Hotkey.Key = "V";
            });

            act.Should().NotThrow($"combination [{string.Join(", ", combination)}] should be valid");
        }
    }

    [Fact]
    public void HotkeyKey_InvalidValue_IsRejected()
    {
        var act = () => _settingsService.UpdateSettings(settings =>
        {
            settings.Hotkey.Modifiers = new List<string> { "Control" };
            settings.Hotkey.Key = "InvalidKey123";
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HotkeyKey_None_IsRejected()
    {
        var act = () => _settingsService.UpdateSettings(settings =>
        {
            settings.Hotkey.Modifiers = new List<string> { "Control" };
            settings.Hotkey.Key = "None";
        });

        act.Should().Throw<ArgumentException>();
    }
}
