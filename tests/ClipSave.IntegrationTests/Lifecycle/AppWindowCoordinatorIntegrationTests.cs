using ClipSave.Infrastructure;
using ClipSave.Infrastructure.Startup;
using ClipSave.Services;
using ClipSave.ViewModels.About;
using ClipSave.ViewModels.Settings;
using ClipSave.Views.Settings;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class AppWindowCoordinatorIntegrationTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _settingsDirectory;
    private readonly ServiceProvider _serviceProvider;
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localizationService;
    private readonly AppHotkeyCoordinator _hotkeyCoordinator;
    private readonly AppWindowCoordinator _windowCoordinator;

    public AppWindowCoordinatorIntegrationTests()
    {
        EnsureApplication();

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _settingsDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_AppWindow_Integration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_settingsDirectory);

        _settingsService = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _settingsDirectory);
        _localizationService = new LocalizationService(NullLogger<LocalizationService>.Instance);
        _localizationService.SetLanguage(AppLanguage.English);

        var notificationService = new NotificationService(
            _loggerFactory.CreateLogger<NotificationService>(),
            _settingsService,
            _localizationService);

        var hotkeyService = new HotkeyService(_loggerFactory.CreateLogger<HotkeyService>());
        _hotkeyCoordinator = new AppHotkeyCoordinator(
            hotkeyService,
            _settingsService,
            notificationService,
            _loggerFactory.CreateLogger("AppHotkeyCoordinator"),
            key => key);

        _serviceProvider = new ServiceCollection()
            .AddSingleton(_settingsService)
            .AddSingleton(_localizationService)
            .AddTransient<AboutViewModel>()
            .AddTransient(provider => new SettingsViewModel(
                _settingsService,
                _localizationService,
                _loggerFactory.CreateLogger<SettingsViewModel>()))
            .BuildServiceProvider();

        _windowCoordinator = new AppWindowCoordinator(
            _serviceProvider,
            _settingsService,
            _localizationService,
            _hotkeyCoordinator,
            _loggerFactory.CreateLogger("AppWindowCoordinator"));
    }

    public void Dispose()
    {
        _windowCoordinator.Dispose();
        _hotkeyCoordinator.Dispose();
        _serviceProvider.Dispose();
        _loggerFactory.Dispose();

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
    [Spec("SPEC-060-003")]
    public void ShowOrActivateAboutWindow_WhenAlreadyOpen_ReusesExistingWindow()
    {
        _windowCoordinator.ShowOrActivateAboutWindow();
        var first = GetPrivateField<Window>(_windowCoordinator, "_aboutWindow");

        _windowCoordinator.ShowOrActivateAboutWindow();
        var second = GetPrivateField<Window>(_windowCoordinator, "_aboutWindow");

        first.Should().NotBeNull();
        second.Should().BeSameAs(first);
    }

    [StaFact]
    [Spec("SPEC-080-005")]
    public void Dispose_ClosesOpenedWindows()
    {
        _windowCoordinator.ShowOrActivateAboutWindow();
        var aboutWindow = GetPrivateField<Window>(_windowCoordinator, "_aboutWindow");
        aboutWindow.Should().NotBeNull();

        var settingsWindow = new SettingsWindow
        {
            DataContext = new SettingsViewModel(
                _settingsService,
                _localizationService,
                _loggerFactory.CreateLogger<SettingsViewModel>())
        };
        settingsWindow.Show();
        SetPrivateField(_windowCoordinator, "_settingsWindow", settingsWindow);

        _windowCoordinator.Dispose();

        aboutWindow!.IsVisible.Should().BeFalse();
        settingsWindow.IsVisible.Should().BeFalse();
    }

    [StaFact]
    [Spec("SPEC-090-007")]
    public void ShowOrActivateSettingsDialog_ReappliesCurrentLanguageBeforeDisplay()
    {
        _settingsService.UpdateSettings(settings => settings.Ui.Language = AppLanguage.Japanese);
        _localizationService.SetLanguage(AppLanguage.English);

        var settingsWindow = new SettingsWindow
        {
            DataContext = new SettingsViewModel(
                _settingsService,
                _localizationService,
                _loggerFactory.CreateLogger<SettingsViewModel>())
        };
        settingsWindow.Show();
        SetPrivateField(_windowCoordinator, "_settingsWindow", settingsWindow);

        _windowCoordinator.ShowOrActivateSettingsDialog();

        _localizationService.CurrentLanguage.Should().Be(AppLanguage.Japanese);
        settingsWindow.Close();
    }

    private static T? GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (T?)field!.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(target, value);
    }

    private static void EnsureApplication()
    {
        if (Application.Current == null)
        {
            _ = new Application();
        }
    }
}
