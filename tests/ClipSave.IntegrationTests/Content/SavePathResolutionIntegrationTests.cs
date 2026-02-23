using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class SavePathResolutionIntegrationTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _settingsDirectory;

    public SavePathResolutionIntegrationTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _settingsDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_SavePath_Integration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_settingsDirectory);
    }

    public void Dispose()
    {
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

    [Fact]
    [Spec("SPEC-000-002")]
    public void SavePipeline_DesktopWindow_ResolvesSaveDirectory()
    {
        using var pipeline = CreatePipeline();
        var targetDirectory = Path.GetTempPath();

        var resolved = pipeline.TryResolveSaveDirectoryForTest(
            new ActiveWindowResult(ActiveWindowKind.Desktop, targetDirectory),
            out var path);

        resolved.Should().BeTrue();
        path.Should().Be(targetDirectory);
    }

    [Fact]
    [Spec("SPEC-000-003")]
    public void SavePipeline_ExplorerWindow_ResolvesSaveDirectory()
    {
        using var pipeline = CreatePipeline();
        var targetDirectory = Path.GetTempPath();

        var resolved = pipeline.TryResolveSaveDirectoryForTest(
            new ActiveWindowResult(ActiveWindowKind.Explorer, targetDirectory),
            out var path);

        resolved.Should().BeTrue();
        path.Should().Be(targetDirectory);
    }

    [Fact]
    [Spec("SPEC-000-008")]
    public void ActiveWindowService_VirtualFolderPath_IsRejected()
    {
        var result = ActiveWindowService.TryNormalizeExistingDirectoryPath(
            "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
            out var normalizedPath);

        result.Should().BeFalse();
        normalizedPath.Should().BeEmpty();
    }

    private SavePipeline CreatePipeline()
    {
        var settingsService = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _settingsDirectory);
        var localizationService = new LocalizationService(_loggerFactory.CreateLogger<LocalizationService>());
        var notificationService = new NotificationService(
            _loggerFactory.CreateLogger<NotificationService>(),
            settingsService,
            localizationService);

        return new SavePipeline(
            _loggerFactory.CreateLogger<SavePipeline>(),
            new ClipboardService(_loggerFactory.CreateLogger<ClipboardService>()),
            new ContentEncodingService(
                _loggerFactory.CreateLogger<ContentEncodingService>(),
                new ImageEncodingService(_loggerFactory.CreateLogger<ImageEncodingService>())),
            new FileStorageService(_loggerFactory.CreateLogger<FileStorageService>()),
            notificationService,
            settingsService,
            new ActiveWindowService(_loggerFactory.CreateLogger<ActiveWindowService>()),
            localizationService);
    }
}
