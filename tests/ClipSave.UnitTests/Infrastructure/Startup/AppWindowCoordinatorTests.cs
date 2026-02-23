using ClipSave.Infrastructure;
using ClipSave.Infrastructure.Startup;
using ClipSave.Services;
using ClipSave.ViewModels.About;
using ClipSave.ViewModels.Settings;
using ClipSave.Views.About;
using ClipSave.Views.Settings;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.IO;

namespace ClipSave.UnitTests;

[UnitTest]
public class AppWindowCoordinatorTests : IDisposable
{
    private readonly string _settingsDirectory;
    private readonly ServiceProvider _serviceProvider;
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localizationService;
    private readonly AppHotkeyCoordinator _hotkeyCoordinator;
    private readonly HotkeyService _hotkeyService;
    private readonly AppWindowCoordinator _windowCoordinator;

    public AppWindowCoordinatorTests()
    {
        _settingsDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ClipSave_AppWindowCoordinatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_settingsDirectory);

        _settingsService = new SettingsService(
            Mock.Of<ILogger<SettingsService>>(),
            _settingsDirectory);
        _localizationService = new LocalizationService(
            Mock.Of<ILogger<LocalizationService>>());
        var notificationService = new NotificationService(
            Mock.Of<ILogger<NotificationService>>(),
            _settingsService,
            _localizationService);
        _hotkeyService = new HotkeyService(
            Mock.Of<ILogger<HotkeyService>>());

        _hotkeyCoordinator = new AppHotkeyCoordinator(
            _hotkeyService,
            _settingsService,
            notificationService,
            NullLogger<AppHotkeyCoordinator>.Instance,
            key => key);

        _serviceProvider = new ServiceCollection()
            .AddSingleton(_settingsService)
            .AddSingleton(_localizationService)
            .AddTransient<SettingsViewModel>()
            .AddTransient<AboutViewModel>()
            .BuildServiceProvider();

        _windowCoordinator = new AppWindowCoordinator(
            _serviceProvider,
            _settingsService,
            _localizationService,
            _hotkeyCoordinator,
            NullLogger<AppWindowCoordinator>.Instance);
    }

    public void Dispose()
    {
        _windowCoordinator.Dispose();
        _hotkeyCoordinator.Dispose();
        _hotkeyService.Dispose();
        _serviceProvider.Dispose();

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

    [StaFact]
    public void ShowOrActivateAboutWindow_WhenWindowAlreadyTracked_ReusesExistingInstance()
    {
        var existingWindow = new AboutWindow
        {
            DataContext = _serviceProvider.GetRequiredService<AboutViewModel>()
        };
        _windowCoordinator.SetTrackedAboutWindowForTest(existingWindow);

        var act = () => _windowCoordinator.ShowOrActivateAboutWindow();

        act.Should().NotThrow();
        _windowCoordinator.AboutWindowForTest.Should().BeSameAs(existingWindow);
    }

    [StaFact]
    public void ShowOrActivateSettingsDialog_WhenWindowAlreadyTracked_ReappliesCurrentLanguage()
    {
        _settingsService.UpdateSettings(settings => settings.Ui.Language = AppLanguage.Japanese);
        _localizationService.SetLanguage(AppLanguage.English);

        var existingWindow = new SettingsWindow
        {
            DataContext = _serviceProvider.GetRequiredService<SettingsViewModel>()
        };
        _windowCoordinator.SetTrackedSettingsWindowForTest(existingWindow);

        _windowCoordinator.ShowOrActivateSettingsDialog();

        _localizationService.CurrentLanguage.Should().Be(AppLanguage.Japanese);
        _windowCoordinator.SettingsWindowForTest.Should().BeSameAs(existingWindow);
    }

    [StaFact]
    public void Dispose_WhenWindowsAreTracked_ClearsWindowReferences()
    {
        var aboutWindow = new AboutWindow
        {
            DataContext = _serviceProvider.GetRequiredService<AboutViewModel>()
        };
        var settingsWindow = new SettingsWindow
        {
            DataContext = _serviceProvider.GetRequiredService<SettingsViewModel>()
        };

        _windowCoordinator.SetTrackedAboutWindowForTest(aboutWindow);
        _windowCoordinator.SetTrackedSettingsWindowForTest(settingsWindow);

        _windowCoordinator.Dispose();

        _windowCoordinator.AboutWindowForTest.Should().BeNull();
        _windowCoordinator.SettingsWindowForTest.Should().BeNull();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var act = () =>
        {
            _windowCoordinator.Dispose();
            _windowCoordinator.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void ShowOrActivateSettingsDialog_AfterDispose_ThrowsObjectDisposedException()
    {
        _windowCoordinator.Dispose();

        var act = () => _windowCoordinator.ShowOrActivateSettingsDialog();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ShowOrActivateAboutWindow_AfterDispose_ThrowsObjectDisposedException()
    {
        _windowCoordinator.Dispose();

        var act = () => _windowCoordinator.ShowOrActivateAboutWindow();

        act.Should().Throw<ObjectDisposedException>();
    }
}
