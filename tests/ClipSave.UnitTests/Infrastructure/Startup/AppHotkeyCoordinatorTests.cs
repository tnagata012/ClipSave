using ClipSave.Infrastructure.Startup;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.IO;

namespace ClipSave.UnitTests;

[Collection("HotkeyTests")]
public class AppHotkeyCoordinatorTests : IDisposable
{
    private readonly string _settingsDirectory;
    private readonly AppHotkeyCoordinator _coordinator;

    public AppHotkeyCoordinatorTests()
    {
        _settingsDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ClipSave_AppHotkeyCoordinatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_settingsDirectory);

        var settingsService = new SettingsService(
            Mock.Of<ILogger<SettingsService>>(),
            _settingsDirectory);
        var notificationService = new NotificationService(
            Mock.Of<ILogger<NotificationService>>(),
            settingsService);
        var hotkeyService = new HotkeyService(
            Mock.Of<ILogger<HotkeyService>>());

        _coordinator = new AppHotkeyCoordinator(
            hotkeyService,
            settingsService,
            notificationService,
            NullLogger<AppHotkeyCoordinator>.Instance,
            key => key);
    }

    public void Dispose()
    {
        _coordinator.Dispose();

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
    public void SuspendHotkeyRegistration_BeforeInitialize_DoesNotThrow()
    {
        var act = () => _coordinator.SuspendHotkeyRegistration();

        act.Should().NotThrow();
    }

    [Fact]
    public void ResumeHotkeyRegistration_BeforeInitialize_DoesNotThrow()
    {
        var act = () => _coordinator.ResumeHotkeyRegistration();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var act = () =>
        {
            _coordinator.Dispose();
            _coordinator.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Initialize_AfterDispose_ThrowsObjectDisposedException()
    {
        _coordinator.Dispose();

        var act = () => _coordinator.Initialize();

        act.Should().Throw<ObjectDisposedException>();
    }
}
