using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Windows;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class JsonIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILoggerFactory _loggerFactory;

    public JsonIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_Json_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    public void Dispose()
    {
        CleanupDirectory(_testDirectory);
        _loggerFactory.Dispose();
    }

    private void CleanupDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch { }
        }
    }

    [Fact]
    [Spec("SPEC-014-001")]
    public async Task JsonContent_SavedAsFormattedJson()
    {
        // Arrange
        var imageLogger = _loggerFactory.CreateLogger<ImageEncodingService>();
        var contentLogger = _loggerFactory.CreateLogger<ContentEncodingService>();
        var fileLogger = _loggerFactory.CreateLogger<FileStorageService>();

        var imageService = new ImageEncodingService(imageLogger);
        var contentService = new ContentEncodingService(contentLogger, imageService);
        var fileService = new FileStorageService(fileLogger);

        var rawJson = """{"name":"test","value":123}""";
        var formatted = """
            {
              "name": "test",
              "value": 123
            }
            """;
        var content = new JsonContent(rawJson, formatted);
        var settings = new SaveSettings();

        // Act
        var (data, extension) = contentService.Encode(content, settings);
        var filePath = await fileService.SaveFileAsync(data, _testDirectory, extension);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        filePath.Should().EndWith(".json");

        var savedContent = File.ReadAllText(filePath, Encoding.UTF8);
        savedContent.Trim().Should().Be(formatted.Trim());
    }

    [Fact]
    [Spec("SPEC-014-002")]
    public async Task JsonText_DetectedAndFormatted()
    {
        var json = """{"name":"test","value":123}""";
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () => Clipboard.SetText(json));

        content.Should().BeOfType<JsonContent>();

        var jsonContent = (JsonContent)content!;
        jsonContent.FormattedJson.Should().Contain("\n");
        jsonContent.FormattedJson.Should().Contain("\"name\"");
    }

    [Fact]
    [Spec("SPEC-014-003")]
    public async Task JsonText_RecognizedWhenStartsWithArray()
    {
        var json = "[1,2,3]";
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () => Clipboard.SetText(json));

        content.Should().BeOfType<JsonContent>();
    }
}

