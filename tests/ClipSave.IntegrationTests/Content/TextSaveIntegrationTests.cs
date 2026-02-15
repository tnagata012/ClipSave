using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Windows;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class TextSaveIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILoggerFactory _loggerFactory;

    public TextSaveIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_TextSave_{Guid.NewGuid()}");
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
    [Spec("SPEC-012-001")]
    public async Task TextContent_SavedAsUtf8Txt()
    {
        // Arrange
        var imageLogger = _loggerFactory.CreateLogger<ImageEncodingService>();
        var contentLogger = _loggerFactory.CreateLogger<ContentEncodingService>();
        var fileLogger = _loggerFactory.CreateLogger<FileStorageService>();

        var imageService = new ImageEncodingService(imageLogger);
        var contentService = new ContentEncodingService(contentLogger, imageService);
        var fileService = new FileStorageService(fileLogger);

        var content = new TextContent("Hello, World!\nこんにちは世界");
        var settings = new SaveSettings();

        // Act
        var (data, extension) = contentService.Encode(content, settings);
        var filePath = await fileService.SaveFileAsync(data, _testDirectory, extension);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        filePath.Should().EndWith(".txt");

        var savedText = File.ReadAllText(filePath, Encoding.UTF8);
        savedText.Should().Contain("Hello, World!");
        savedText.Should().Contain("こんにちは世界");
    }

    [Fact]
    [Spec("SPEC-012-002")]
    public async Task WhitespaceText_NotSaved()
    {
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () => Clipboard.SetText("   \n\t  "));

        content.Should().BeNull();
    }
}

