using ClipSave.Infrastructure.Startup;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.IO;

namespace ClipSave.UnitTests;

[Collection("HotkeyTests")]
public class AppWindowCoordinatorTests : IDisposable
{
    private readonly string _settingsDirectory;
    private readonly AppHotkeyCoordinator _hotkeyCoordinator;
    private readonly AppWindowCoordinator _windowCoordinator;

    public AppWindowCoordinatorTests()
    {
        _settingsDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ClipSave_AppWindowCoordinatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_settingsDirectory);

        var settingsService = new SettingsService(
            Mock.Of<ILogger<SettingsService>>(),
            _settingsDirectory);
        var localizationService = new LocalizationService(
            Mock.Of<ILogger<LocalizationService>>());
        var notificationService = new NotificationService(
            Mock.Of<ILogger<NotificationService>>(),
            settingsService,
            localizationService);
        var hotkeyService = new HotkeyService(
            Mock.Of<ILogger<HotkeyService>>());

        _hotkeyCoordinator = new AppHotkeyCoordinator(
            hotkeyService,
            settingsService,
            notificationService,
            NullLogger<AppHotkeyCoordinator>.Instance,
            key => key);

        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        _windowCoordinator = new AppWindowCoordinator(
            serviceProvider,
            settingsService,
            localizationService,
            _hotkeyCoordinator,
            NullLogger<AppWindowCoordinator>.Instance);
    }

    public void Dispose()
    {
        _windowCoordinator.Dispose();
        _hotkeyCoordinator.Dispose();

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
