using ClipSave.Infrastructure;
using ClipSave.Services;
using ClipSave.ViewModels.Settings;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;

namespace ClipSave.UiTests;

[UiTest]
public class TrayServiceUiTests
{
    [StaFact]
    [Spec("SPEC-021-002")]
    public void ContextMenu_ContainsRequiredEntries()
    {
        WpfTestHost.EnsureApplication();

        var localization = new LocalizationService(NullLogger<LocalizationService>.Instance);
        localization.SetLanguage(AppLanguage.English);

        using var trayService = new TrayService(NullLogger<TrayService>.Instance, localization);
        var menuText = trayService.GetMenuTextSnapshotForTest();

        menuText.Settings.Should().Be(localization.GetString("Tray_Menu_Settings"));
        menuText.StartupSettings.Should().Be(localization.GetString("Tray_Menu_StartupSettings"));
        menuText.NotificationSettings.Should().Be(localization.GetString("Tray_Menu_NotificationSettings"));
        menuText.About.Should().Be(localization.GetString("Tray_Menu_About"));
        menuText.Exit.Should().Be(localization.GetString("Tray_Menu_Exit"));
    }

    [StaFact]
    [Spec("SPEC-021-006")]
    public void AboutMenu_Click_RaisesAboutRequested()
    {
        WpfTestHost.EnsureApplication();

        var localization = new LocalizationService(NullLogger<LocalizationService>.Instance);
        localization.SetLanguage(AppLanguage.English);

        using var trayService = new TrayService(NullLogger<TrayService>.Instance, localization);

        var raised = false;
        trayService.AboutRequested += (_, _) => raised = true;

        trayService.TriggerAboutMenuForTest();

        raised.Should().BeTrue();
    }

    [StaFact]
    [Spec("SPEC-021-010")]
    public void ContextMenu_UsesCurrentUiLanguage()
    {
        WpfTestHost.EnsureApplication();

        var localization = new LocalizationService(NullLogger<LocalizationService>.Instance);
        localization.SetLanguage(AppLanguage.Japanese);

        using var trayService = new TrayService(NullLogger<TrayService>.Instance, localization);
        var menuText = trayService.GetMenuTextSnapshotForTest();

        menuText.Settings.Should().Be(localization.GetString("Tray_Menu_Settings"));
        menuText.Exit.Should().Be(localization.GetString("Tray_Menu_Exit"));
    }

    [StaFact]
    [Spec("SPEC-090-006")]
    public void LanguageChange_AppliesToTrayMenuWithoutRestart()
    {
        WpfTestHost.EnsureApplication();

        var testDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_TrayUiLanguage_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);

        try
        {
            var settingsService = new SettingsService(NullLogger<SettingsService>.Instance, testDirectory);
            var localization = new LocalizationService(NullLogger<LocalizationService>.Instance);
            localization.SetLanguage(AppLanguage.English);
            settingsService.UpdateSettings(settings => settings.Ui.Language = AppLanguage.English);

            settingsService.SettingsChanged += (_, settings) =>
            {
                localization.SetLanguage(settings.Ui.Language);
            };

            var viewModel = new SettingsViewModel(
                settingsService,
                localization,
                NullLogger<SettingsViewModel>.Instance);

            using var trayService = new TrayService(NullLogger<TrayService>.Instance, localization);
            var before = trayService.GetMenuTextSnapshotForTest().Settings;

            viewModel.SelectedLanguage = AppLanguage.Japanese;
            viewModel.SaveCommand.Execute(null);
            WpfTestHost.FlushEvents();

            var after = trayService.GetMenuTextSnapshotForTest().Settings;
            settingsService.Current.Ui.Language.Should().Be(AppLanguage.Japanese);
            after.Should().Be(localization.GetString("Tray_Menu_Settings"));
            after.Should().NotBe(before);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                try
                {
                    Directory.Delete(testDirectory, true);
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }
        }
    }
}
