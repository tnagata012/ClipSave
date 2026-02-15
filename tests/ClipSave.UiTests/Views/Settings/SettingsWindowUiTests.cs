using ClipSave.Infrastructure;
using ClipSave.Services;
using ClipSave.ViewModels.Settings;
using ClipSave.Views.Settings;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClipSave.UiTests;

[UiTest]
public class SettingsWindowUiTests
{
    [StaFact]
    [Spec("SPEC-040-007")]
    public void SaveButton_IsEnabledOnlyWhenUnsavedChangesExist()
    {
        using var context = CreateContext();

        var saveButton = FindSaveButton(context.Window, context.ViewModel);

        saveButton.IsEnabled.Should().BeFalse();

        context.ViewModel.JpegQuality = 80;
        WpfTestHost.FlushEvents();

        saveButton.IsEnabled.Should().BeTrue();
    }

    [StaFact]
    [Spec("SPEC-040-004")]
    public void ValidationError_IsDisplayedInStatusArea()
    {
        using var context = CreateContext();

        context.ViewModel.ImageEnabled = false;
        context.ViewModel.TextEnabled = false;
        context.ViewModel.MarkdownEnabled = false;
        context.ViewModel.JsonEnabled = false;
        context.ViewModel.CsvEnabled = false;

        context.ViewModel.SaveCommand.Execute(null);
        WpfTestHost.FlushEvents();

        context.ViewModel.HasError.Should().BeTrue();
        context.ViewModel.StatusMessage.Should().NotBeNullOrWhiteSpace();

        var errorText = LogicalTreeSearch.FindDescendants<TextBlock>(context.Window)
            .FirstOrDefault(textBlock => textBlock.Text == context.ViewModel.StatusMessage);

        errorText.Should().NotBeNull();
        errorText!.Visibility.Should().Be(Visibility.Visible);
    }

    [StaFact]
    [Spec("SPEC-040-008")]
    public void InputBindings_MapSaveAndCloseShortcuts()
    {
        using var context = CreateContext();
        var inputBindings = context.Window.InputBindings.OfType<KeyBinding>().ToList();

        inputBindings.Should().Contain(binding =>
            binding.Key == Key.S &&
            binding.Modifiers == ModifierKeys.Control &&
            ReferenceEquals(binding.Command, context.ViewModel.SaveCommand));

        inputBindings.Should().Contain(binding =>
            binding.Key == Key.W &&
            binding.Modifiers == ModifierKeys.Control &&
            ReferenceEquals(binding.Command, context.ViewModel.CancelCommand));

        inputBindings.Should().Contain(binding =>
            binding.Key == Key.Escape &&
            binding.Modifiers == ModifierKeys.None &&
            ReferenceEquals(binding.Command, context.ViewModel.CancelCommand));
    }

    [StaFact]
    [Spec("SPEC-090-004")]
    public void LanguageSelection_AllowsEnglishAndJapanese()
    {
        using var context = CreateContext();
        SelectTabByHeader(
            context.Window,
            context.ViewModel.Localizer.GetString("SettingsWindow_Tab_Language"));

        var languageComboBox = LogicalTreeSearch.FindDescendants<ComboBox>(context.Window)
            .Single(comboBox =>
                comboBox.DisplayMemberPath == "DisplayName" &&
                comboBox.SelectedValuePath == "Code");

        var optionCodes = languageComboBox.Items
            .OfType<LanguageOptionItem>()
            .Select(item => item.Code)
            .ToArray();

        optionCodes.Should().BeEquivalentTo(new[] { AppLanguage.English, AppLanguage.Japanese });

        languageComboBox.SelectedValue = AppLanguage.Japanese;
        WpfTestHost.FlushEvents();

        context.ViewModel.SelectedLanguage.Should().Be(AppLanguage.Japanese);
    }

    private static Button FindSaveButton(SettingsWindow window, SettingsViewModel viewModel)
    {
        return LogicalTreeSearch.FindDescendants<Button>(window)
            .Single(button => ReferenceEquals(button.Command, viewModel.SaveCommand));
    }

    private static void SelectTabByHeader(SettingsWindow window, string tabHeader)
    {
        var tabControl = LogicalTreeSearch.FindDescendants<TabControl>(window).Single();
        var tabItem = tabControl.Items
            .OfType<TabItem>()
            .Single(item => string.Equals(item.Header?.ToString(), tabHeader, StringComparison.Ordinal));

        tabControl.SelectedItem = tabItem;
        WpfTestHost.FlushEvents();
    }

    private static TestContext CreateContext()
    {
        WpfTestHost.EnsureApplication();

        var testDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_SettingsUiTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);

        var settingsService = new SettingsService(NullLogger<SettingsService>.Instance, testDirectory);
        var localization = new LocalizationService(NullLogger<LocalizationService>.Instance);
        localization.SetLanguage(AppLanguage.English);

        var viewModel = new SettingsViewModel(
            settingsService,
            localization,
            NullLogger<SettingsViewModel>.Instance);

        var window = new SettingsWindow
        {
            DataContext = viewModel
        };
        window.Show();
        WpfTestHost.FlushEvents();

        return new TestContext(testDirectory, window, viewModel);
    }

    private sealed class TestContext : IDisposable
    {
        public TestContext(string testDirectory, SettingsWindow window, SettingsViewModel viewModel)
        {
            TestDirectory = testDirectory;
            Window = window;
            ViewModel = viewModel;
        }

        public string TestDirectory { get; }
        public SettingsWindow Window { get; }
        public SettingsViewModel ViewModel { get; }

        public void Dispose()
        {
            Window.DataContext = null;

            if (Window.IsVisible)
            {
                Window.Close();
                WpfTestHost.FlushEvents();
            }

            if (Directory.Exists(TestDirectory))
            {
                try
                {
                    Directory.Delete(TestDirectory, true);
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }
        }
    }
}
