using ClipSave.Infrastructure;
using ClipSave.Services;
using ClipSave.ViewModels.About;
using ClipSave.Views.About;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ClipSave.UiTests;

[UiTest]
public class AboutWindowUiTests
{
    [StaFact]
    [Spec("SPEC-060-001")]
    [Spec("SPEC-060-004")]
    public void AboutWindow_DisplaysSystemAndVersionInformation()
    {
        using var context = CreateContext();
        var textBlocks = LogicalTreeSearch.FindDescendants<TextBlock>(context.Window);

        var textValues = textBlocks
            .Select(textBlock => textBlock.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
        var inlineTexts = textBlocks
            .SelectMany(textBlock => textBlock.Inlines.OfType<Run>())
            .Select(run => run.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        textValues.Should().Contain(context.ViewModel.ApplicationName);
        textValues.Should().Contain(context.ViewModel.InformationalVersion);
        textValues.Should().Contain(context.ViewModel.DotNetVersion);
        textValues.Should().Contain(context.ViewModel.OsVersion);
        textValues.Should().Contain(context.ViewModel.BuildDate);
        textValues.Should().Contain(context.ViewModel.Copyright);
        context.ViewModel.Version.Should().Match(v =>
            v == "Unknown" ||
            System.Text.RegularExpressions.Regex.IsMatch(v, @"^\d+\.\d+\.\d+$"));
        context.ViewModel.InformationalVersion.Should().Match(v =>
            v == "Unknown" ||
            System.Text.RegularExpressions.Regex.IsMatch(
                v,
                @"^\d+\.\d+\.\d+(\.local|-[0-9A-Za-z\.-]+)?(\+sha\.[0-9a-f]{7})?$"));
        inlineTexts.Should().Contain(context.ViewModel.Version);
    }

    [StaFact]
    [Spec("SPEC-060-002")]
    public void CloseButton_ClosesAboutWindow()
    {
        using var context = CreateContext();

        var closeButton = LogicalTreeSearch.FindDescendants<Button>(context.Window)
            .Single(button => ReferenceEquals(button.Command, context.ViewModel.CloseCommand));

        closeButton.Command.Should().NotBeNull();
        closeButton.Command!.Execute(null);
        WpfTestHost.FlushEvents();

        context.Window.IsVisible.Should().BeFalse();
    }

    private static TestContext CreateContext()
    {
        WpfTestHost.EnsureApplication();

        var localization = new LocalizationService(NullLogger<LocalizationService>.Instance);
        localization.SetLanguage(AppLanguage.English);

        var viewModel = new AboutViewModel(localization);
        var window = new AboutWindow
        {
            DataContext = viewModel
        };

        window.Show();
        WpfTestHost.FlushEvents();

        return new TestContext(window, viewModel);
    }

    private sealed class TestContext : IDisposable
    {
        public TestContext(AboutWindow window, AboutViewModel viewModel)
        {
            Window = window;
            ViewModel = viewModel;
        }

        public AboutWindow Window { get; }
        public AboutViewModel ViewModel { get; }

        public void Dispose()
        {
            if (Window.IsVisible)
            {
                Window.Close();
                WpfTestHost.FlushEvents();
            }
        }
    }
}
