using ClipSave.Infrastructure;
using ClipSave.Infrastructure.Startup;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using System.IO;
using System.Windows.Threading;
using System.Xml.Linq;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class StartupAndLanguageIntegrationTests : IDisposable
{
    private readonly string _settingsDirectory;

    public StartupAndLanguageIntegrationTests()
    {
        _settingsDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_Startup_Integration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_settingsDirectory);
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
    [Spec("SPEC-000-001")]
    public void AppXaml_DoesNotDeclareStartupWindow()
    {
        var appXamlPath = GetProjectPath("src", "ClipSave", "App.xaml");
        var xaml = File.ReadAllText(appXamlPath);

        xaml.Should().Contain("ShutdownMode=\"OnExplicitShutdown\"");
        xaml.Should().NotContain("StartupUri=");
    }

    [Fact]
    [Spec("SPEC-041-001")]
    [Spec("SPEC-041-004")]
    public void PackageManifest_StartupTask_IsEnabledByDefault()
    {
        var manifestPath = GetProjectPath("src", "ClipSave.Package", "Package.appxmanifest");
        var document = XDocument.Load(manifestPath);
        XNamespace desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";

        var startupTask = document.Descendants(desktop + "StartupTask").SingleOrDefault();

        startupTask.Should().NotBeNull();
        startupTask!.Attribute("Enabled")?.Value.Should().Be("true");
    }

    [Fact]
    [Spec("SPEC-041-002")]
    [Spec("SPEC-050-002")]
    public void SettingsWindow_DoesNotExposeStartupOrGlobalNotificationToggle()
    {
        var xamlPath = GetProjectPath("src", "ClipSave", "Views", "Settings", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(xamlPath);

        xaml.Should().NotContain("SettingsWindow_Startup", "startup setting should be managed by Windows");
        xaml.Should().Contain("SettingsWindow_Notification_GlobalHint");
        xaml.Should().NotContain("NotifyEnabled");
        xaml.Should().NotContain("NotificationEnabled");
    }

    [StaFact]
    [Spec("SPEC-041-003")]
    [Spec("SPEC-041-005")]
    public void StartupGuidance_IsPersistedAndShownOnlyOnFirstRun()
    {
        var settingsService = new SettingsService(NullLogger<SettingsService>.Instance, _settingsDirectory);
        settingsService.UpdateSettings(settings => settings.Advanced.StartupGuidanceShown = false);
        var settingsChangedCount = 0;
        settingsService.SettingsChanged += (_, _) => settingsChangedCount++;

        var coordinator = new AppLifecycleCoordinator(
            new ServiceCollection().BuildServiceProvider(),
            Dispatcher.CurrentDispatcher,
            NullLogger.Instance,
            () => { });

        try
        {
            coordinator.SetSettingsServiceForTest(settingsService);

            coordinator.ShowStartupGuidanceIfNeededForTest();
            coordinator.ShowStartupGuidanceIfNeededForTest();

            settingsService.Current.Advanced.StartupGuidanceShown.Should().BeTrue();
            settingsChangedCount.Should().Be(1);
        }
        finally
        {
            coordinator.Dispose();
        }
    }

    [Fact]
    [Spec("SPEC-090-001")]
    public void LanguageNormalization_OnlyEnglishAndJapaneseAreSupported()
    {
        AppLanguage.Normalize("en", useSystemWhenMissing: false).Should().Be(AppLanguage.English);
        AppLanguage.Normalize("en-US", useSystemWhenMissing: false).Should().Be(AppLanguage.English);
        AppLanguage.Normalize("ja", useSystemWhenMissing: false).Should().Be(AppLanguage.Japanese);
        AppLanguage.Normalize("ja-JP", useSystemWhenMissing: false).Should().Be(AppLanguage.Japanese);
        AppLanguage.Normalize("fr-FR", useSystemWhenMissing: false).Should().Be(AppLanguage.English);
    }

    [Fact]
    [Spec("SPEC-090-002")]
    public void LanguageDefault_UsesSystemCultureFallbackRule()
    {
        AppLanguage.ResolveFromSystem(CultureInfo.GetCultureInfo("ja-JP"))
            .Should().Be(AppLanguage.Japanese);
        AppLanguage.ResolveFromSystem(CultureInfo.GetCultureInfo("en-US"))
            .Should().Be(AppLanguage.English);
        AppLanguage.ResolveFromSystem(CultureInfo.GetCultureInfo("fr-FR"))
            .Should().Be(AppLanguage.English);
    }

    private static string GetProjectPath(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var parts = new string[segments.Length + 1];
        parts[0] = root;
        Array.Copy(segments, 0, parts, 1, segments.Length);
        return Path.Combine(parts);
    }
}
