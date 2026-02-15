using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Windows;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class MarkdownIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILoggerFactory _loggerFactory;

    public MarkdownIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_Markdown_{Guid.NewGuid()}");
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
    [Spec("SPEC-013-001")]
    public async Task MarkdownContent_SavedAsMd()
    {
        // Arrange
        var imageLogger = _loggerFactory.CreateLogger<ImageEncodingService>();
        var contentLogger = _loggerFactory.CreateLogger<ContentEncodingService>();
        var fileLogger = _loggerFactory.CreateLogger<FileStorageService>();

        var imageService = new ImageEncodingService(imageLogger);
        var contentService = new ContentEncodingService(contentLogger, imageService);
        var fileService = new FileStorageService(fileLogger);

        var markdown = """
            # Hello World
            
            This is a **test** document.
            
            - Item 1
            - Item 2
            """;
        var content = new MarkdownContent(markdown);
        var settings = new SaveSettings();

        // Act
        var (data, extension) = contentService.Encode(content, settings);
        var filePath = await fileService.SaveFileAsync(data, _testDirectory, extension);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        filePath.Should().EndWith(".md");

        var savedContent = File.ReadAllText(filePath, Encoding.UTF8);
        savedContent.Should().Contain("# Hello World");
        savedContent.Should().Contain("**test**");
    }

    [Fact]
    [Spec("SPEC-013-002")]
    public async Task MarkdownPattern_DetectedAsMarkdown()
    {
        var markdown = """
            # Heading

            - Item 1
            - Item 2
            """;
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () => Clipboard.SetText(markdown));

        content.Should().BeOfType<MarkdownContent>();
    }

    [Fact]
    [Spec("SPEC-013-002")]
    public async Task SingleMarkdownPattern_DetectedAsMarkdown()
    {
        var markdown = "# Heading Only";
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () => Clipboard.SetText(markdown));

        content.Should().BeOfType<MarkdownContent>();
    }
}

