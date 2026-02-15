using ClipSave.Infrastructure.Startup;
using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class AppServiceProviderFactoryIntegrationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _settingsPath;
    private readonly string _logDirectory;

    public AppServiceProviderFactoryIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"ClipSave_Startup_Integration_{Guid.NewGuid()}");
        _settingsPath = Path.Combine(_testRoot, "settings.json");
        _logDirectory = Path.Combine(_testRoot, "logs");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            try
            {
                Directory.Delete(_testRoot, true);
            }
            catch
            {
                // cleanup error is ignored
            }
        }
    }

    [Fact]
    [Spec("SPEC-042-006")]
    public void LoggingEnabled_ConfiguresFileLoggerWithSevenFileRetention()
    {
        // Arrange
        WriteSettings(loggingEnabled: true);

        var invocationCount = 0;
        string? configuredFilePath = null;
        int? configuredRetainedFileCountLimit = null;
        LogLevel? configuredMinimumLevel = null;

        // Act
        using var provider = (ServiceProvider)AppServiceProviderFactory.CreateServiceProvider(
            _settingsPath,
            _logDirectory,
            (_, filePath, retainedFileCountLimit, minimumLevel) =>
            {
                invocationCount++;
                configuredFilePath = filePath;
                configuredRetainedFileCountLimit = retainedFileCountLimit;
                configuredMinimumLevel = minimumLevel;
            });

        // Assert
        invocationCount.Should().Be(1);
        Directory.Exists(_logDirectory).Should().BeTrue();
        configuredFilePath.Should().Be(Path.Combine(_logDirectory, "clipsave-{Date}.log"));
        configuredRetainedFileCountLimit.Should().Be(AppServiceProviderFactory.LogRetainedFileCountLimit);
        configuredRetainedFileCountLimit.Should().Be(7);
        configuredMinimumLevel.Should().Be(LogLevel.Debug);
    }

    [Fact]
    public void LoggingDisabled_DoesNotConfigureFileLogger()
    {
        // Arrange
        WriteSettings(loggingEnabled: false);

        var invocationCount = 0;

        // Act
        using var provider = (ServiceProvider)AppServiceProviderFactory.CreateServiceProvider(
            _settingsPath,
            _logDirectory,
            (_, _, _, _) => invocationCount++);

        // Assert
        invocationCount.Should().Be(0);
        Directory.Exists(_logDirectory).Should().BeFalse();
    }

    private void WriteSettings(bool loggingEnabled)
    {
        var settings = new AppSettings();
        settings.Advanced.Logging = loggingEnabled;

        var json = JsonSerializer.Serialize(settings, SettingsService.CreateJsonOptions());
        File.WriteAllText(_settingsPath, json);
    }
}
