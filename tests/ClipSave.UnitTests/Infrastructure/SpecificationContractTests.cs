using FluentAssertions;
using System.IO;
using System.Xml.Linq;

namespace ClipSave.UnitTests;

[UnitTest]
public class SpecificationContractTests
{
    [Fact]
    public void AppXaml_UsesExplicitShutdownWithoutStartupWindow()
    {
        var appXamlPath = Path.Combine(TestPaths.SourceRoot, "App.xaml");
        var xaml = File.ReadAllText(appXamlPath);

        xaml.Should().Contain("ShutdownMode=\"OnExplicitShutdown\"");
        xaml.Should().NotContain("StartupUri=");
    }

    [Fact]
    public void ActiveWindowService_HandlesDesktopAndExplorerBranches()
    {
        var source = ReadClipSaveSource("Services", "Platform", "ActiveWindowService.cs");

        source.Should().Contain("if (IsDesktopClassName(className))");
        source.Should().Contain("Environment.SpecialFolder.DesktopDirectory");
        source.Should().Contain("if (IsExplorerClassName(className))");
        source.Should().Contain("GetExplorerPath(hWnd)");
    }

    [Fact]
    public void AppHotkeyCoordinator_RegistrationFailurePath_RestoresPreviousHotkey()
    {
        var source = ReadClipSaveSource("Infrastructure", "Startup", "AppHotkeyCoordinator.cs");

        source.Should().Contain("RestorePreviousHotkey();");
        source.Should().Contain("var restored = _hotkeyService.Register(fallback.Modifiers, fallback.Key);");
        source.Should().Contain("_settingsService.UpdateSettings(settings => settings.Hotkey = CloneHotkeySettings(fallback));");
    }

    [Fact]
    public void SavePipeline_RuntimeFailures_AreConvertedToErrorResultAndNotified()
    {
        var source = ReadClipSaveSource("Services", "Pipeline", "SavePipeline.cs");

        source.Should().Contain("catch (Exception ex)");
        source.Should().Contain("var errorResult = SaveResult.CreateFailure");
        source.Should().Contain("NotifyResultSafely(errorResult);");
    }

    [Fact]
    public void ClipboardService_ClipboardLockRetryPolicy_IsDefined()
    {
        var source = ReadClipSaveSource("Services", "Platform", "ClipboardService.cs");

        source.Should().Contain("MaxRetries = 3");
        source.Should().Contain("RetryDelayMs = 50");
        source.Should().Contain("attempt >= MaxRetries");
        source.Should().Contain("Task.Delay(RetryDelayMs)");
    }

    [Fact]
    public void SettingsWindow_UnsavedChangesPrompt_IsImplemented()
    {
        var source = ReadClipSaveSource("Views", "Settings", "SettingsWindow.xaml.cs");

        source.Should().Contain("if (DataContext is SettingsViewModel vm && vm.IsDirty)");
        source.Should().Contain("ShowCloseConfirmation()");
        source.Should().Contain("MessageBoxButton.YesNo");
    }

    [Fact]
    public void PackageManifest_StartupTask_IsEnabledByDefault()
    {
        var manifestPath = Path.Combine(TestPaths.RepositoryRoot, "src", "ClipSave.Package", "Package.appxmanifest");
        var document = XDocument.Load(manifestPath);
        XNamespace desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";

        var startupTask = document.Descendants(desktop + "StartupTask").SingleOrDefault();

        startupTask.Should().NotBeNull();
        startupTask!.Attribute("Enabled")?.Value.Should().Be("true");
    }

    [Fact]
    public void AppLifecycleCoordinator_StartupGuidance_IsShownOnlyOnFirstRun()
    {
        var source = ReadClipSaveSource("Infrastructure", "Startup", "AppLifecycleCoordinator.cs");

        source.Should().Contain("if (_settingsService?.Current.Advanced.StartupGuidanceShown == true)");
        source.Should().Contain("_trayService?.ShowBalloonNotification(GetLocalizedString(\"App_StartupGuidance\"));");
        source.Should().Contain("_settingsService?.UpdateSettings(settings => settings.Advanced.StartupGuidanceShown = true);");
    }

    [Fact]
    public void TrayService_UsesBalloonTipApi_ForNotifications()
    {
        var source = ReadClipSaveSource("Services", "Platform", "TrayService.cs");

        source.Should().Contain("_notifyIcon.ShowBalloonTip");
    }

    [Fact]
    public void AppWindowCoordinator_ReusesAboutWindowInstance_WhenAlreadyOpen()
    {
        var source = ReadClipSaveSource("Infrastructure", "Startup", "AppWindowCoordinator.cs");

        source.Should().Contain("if (_aboutWindow != null)");
        source.Should().Contain("_aboutWindow.Activate();");
        source.Should().Contain("return;");
    }

    [Fact]
    public void App_CrashDialog_UsesLocalizedTemplateWithDumpPath()
    {
        var source = ReadClipSaveSource("App.xaml.cs");

        source.Should().Contain("FormatLocalizedString(\"App_CrashDialogMessage\", dumpPath)");
        source.Should().Contain("GetLocalizedString(\"App_CrashDialogTitle\")");
        source.Should().Contain("MessageBoxImage.Error");
    }

    [Fact]
    public void AppWindowCoordinator_Dispose_ClosesManagedWindows()
    {
        var source = ReadClipSaveSource("Infrastructure", "Startup", "AppWindowCoordinator.cs");

        source.Should().Contain("_settingsWindow.Close();");
        source.Should().Contain("_aboutWindow.Close();");
    }

    [Fact]
    public void App_StartupFailureAndExit_CleanupPaths_AreDefined()
    {
        var source = ReadClipSaveSource("App.xaml.cs");

        source.Should().Contain("An error occurred during startup");
        source.Should().Contain("Shutdown();");
        source.Should().Contain("_lifecycleCoordinator?.Dispose();");
        source.Should().Contain("if (_serviceProvider is IDisposable disposable)");
    }

    [Fact]
    public void AppWindowCoordinator_SettingsWindowOpen_ReappliesCurrentLanguage()
    {
        var source = ReadClipSaveSource("Infrastructure", "Startup", "AppWindowCoordinator.cs");

        source.Should().Contain("_localizationService.SetLanguage(_settingsService.Current.Ui.Language);");
    }

    private static string ReadClipSaveSource(params string[] relativeSegments)
    {
        var parts = new string[relativeSegments.Length + 1];
        parts[0] = TestPaths.SourceRoot;
        Array.Copy(relativeSegments, 0, parts, 1, relativeSegments.Length);
        var path = Path.Combine(parts);
        return File.ReadAllText(path);
    }
}
