using FluentAssertions;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ClipSave.UnitTests;

[UnitTest]
public class SettingsWindowXamlTests
{
    [Fact]
    public void KeyboardShortcuts_AreDefined()
    {
        var xamlPath = GetSettingsWindowXamlPath();
        File.Exists(xamlPath).Should().BeTrue();

        var document = XDocument.Load(xamlPath);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var keyBindings = document.Descendants(presentation + "KeyBinding").ToList();

        keyBindings.Should().Contain(binding =>
            (string?)binding.Attribute("Key") == "S" &&
            (string?)binding.Attribute("Modifiers") == "Control" &&
            (string?)binding.Attribute("Command") == "{Binding SaveCommand}");

        keyBindings.Should().Contain(binding =>
            (string?)binding.Attribute("Key") == "W" &&
            (string?)binding.Attribute("Modifiers") == "Control" &&
            (string?)binding.Attribute("Command") == "{Binding CancelCommand}");

        keyBindings.Should().Contain(binding =>
            (string?)binding.Attribute("Key") == "Escape" &&
            (string?)binding.Attribute("Command") == "{Binding CancelCommand}");
    }

    [Fact]
    public void LocalizerBindings_AreOneWay()
    {
        var xaml = File.ReadAllText(GetSettingsWindowXamlPath());

        var bindings = Regex.Matches(xaml, @"\{Binding\s+Localizer\[[^\]]+\][^}]*\}")
            .Select(match => match.Value)
            .ToList();

        bindings.Should().NotBeEmpty();
        bindings.Should().OnlyContain(binding => binding.Contains("Mode=OneWay", StringComparison.Ordinal));
    }

    [Fact]
    public void LanguageTab_IsDefined()
    {
        var document = XDocument.Load(GetSettingsWindowXamlPath());
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var tabHeaders = document
            .Descendants(presentation + "TabItem")
            .Select(item => (string?)item.Attribute("Header"))
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .ToList();

        tabHeaders.Should().Contain("{Binding Localizer[SettingsWindow_Tab_Language], Mode=OneWay}");

        var applyHintTextBlocks = document
            .Descendants(presentation + "TextBlock")
            .Select(block => (string?)block.Attribute("Text"))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        applyHintTextBlocks.Should().Contain("{Binding Localizer[SettingsWindow_Language_ApplyHint], Mode=OneWay}");
    }

    [Fact]
    public void StartupToggleAndGlobalNotificationToggle_AreNotDefined()
    {
        var xaml = File.ReadAllText(GetSettingsWindowXamlPath());

        xaml.Should().NotContain("SettingsWindow_Startup", "startup configuration must be managed by Windows");
        xaml.Should().Contain("SettingsWindow_Notification_GlobalHint");
        xaml.Should().NotContain("NotifyEnabled");
        xaml.Should().NotContain("NotificationEnabled");
    }

    private static string GetSettingsWindowXamlPath()
    {
        return Path.Combine(TestPaths.SourceRoot, "Views", "Settings", "SettingsWindow.xaml");
    }
}
