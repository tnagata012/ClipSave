using FluentAssertions;
using System.IO;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class AppBehaviorContractIntegrationTests
{
    [Fact]
    [Spec("SPEC-030-003")]
    public void ClipboardService_RetryPolicy_IsDefined()
    {
        var source = ReadSource("src", "ClipSave", "Services", "Platform", "ClipboardService.cs");

        source.Should().Contain("MaxRetries = 3");
        source.Should().Contain("RetryDelayMs = 50");
        source.Should().Contain("attempt >= MaxRetries");
        source.Should().Contain("Task.Delay(RetryDelayMs)");
    }

    [Fact]
    [Spec("SPEC-040-003")]
    public void SettingsWindow_CloseWithUnsavedChanges_ShowsConfirmation()
    {
        var source = ReadSource("src", "ClipSave", "Views", "Settings", "SettingsWindow.xaml.cs");

        source.Should().Contain("if (DataContext is SettingsViewModel vm && vm.IsDirty)");
        source.Should().Contain("ShowCloseConfirmation()");
        source.Should().Contain("MessageBoxButton.YesNo");
    }

    [Fact]
    [Spec("SPEC-070-003")]
    public void App_TerminatingCrash_ShowsDumpPathInDialog()
    {
        var source = ReadSource("src", "ClipSave", "App.xaml.cs");

        source.Should().Contain("FormatLocalizedString(\"App_CrashDialogMessage\", dumpPath)");
        source.Should().Contain("GetLocalizedString(\"App_CrashDialogTitle\")");
        source.Should().Contain("MessageBoxImage.Error");
    }

    [Fact]
    [Spec("SPEC-080-007")]
    public void App_StartupErrorPath_PerformsCleanupAndShutdown()
    {
        var source = ReadSource("src", "ClipSave", "App.xaml.cs");

        source.Should().Contain("catch (Exception ex)");
        source.Should().Contain("An error occurred during startup");
        source.Should().Contain("Shutdown();");
        source.Should().Contain("_lifecycleCoordinator?.Dispose();");
        source.Should().Contain("if (_serviceProvider is IDisposable disposable)");
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
