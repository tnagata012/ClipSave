using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Windows;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class CsvIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILoggerFactory _loggerFactory;

    public CsvIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_Csv_{Guid.NewGuid()}");
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
    [Spec("SPEC-015-002")]
    public async Task CsvContent_TabsConvertedToCommas()
    {
        // Arrange
        var imageLogger = _loggerFactory.CreateLogger<ImageEncodingService>();
        var contentLogger = _loggerFactory.CreateLogger<ContentEncodingService>();
        var fileLogger = _loggerFactory.CreateLogger<FileStorageService>();

        var imageService = new ImageEncodingService(imageLogger);
        var contentService = new ContentEncodingService(contentLogger, imageService);
        var fileService = new FileStorageService(fileLogger);

        var tabText = "Name\tAge\tCity\nAlice\t30\tTokyo\nBob\t25\tOsaka";
        var content = new CsvContent(tabText, 3, 3);
        var settings = new SaveSettings();

        // Act
        var (data, extension) = contentService.Encode(content, settings);
        var filePath = await fileService.SaveFileAsync(data, _testDirectory, extension);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        filePath.Should().EndWith(".csv");

        var savedContent = File.ReadAllText(filePath, Encoding.UTF8);
        savedContent.Should().Contain("Name,Age,City");
        savedContent.Should().Contain("Alice,30,Tokyo");
        savedContent.Should().Contain("Bob,25,Osaka");
    }

    [Fact]
    [Spec("SPEC-015-003")]
    public async Task CsvContent_HasBomForExcelCompatibility()
    {
        // Arrange
        var imageLogger = _loggerFactory.CreateLogger<ImageEncodingService>();
        var contentLogger = _loggerFactory.CreateLogger<ContentEncodingService>();
        var fileLogger = _loggerFactory.CreateLogger<FileStorageService>();

        var imageService = new ImageEncodingService(imageLogger);
        var contentService = new ContentEncodingService(contentLogger, imageService);
        var fileService = new FileStorageService(fileLogger);

        var tabText = "A\tB\n1\t2";
        var content = new CsvContent(tabText, 2, 2);
        var settings = new SaveSettings();

        // Act
        var (data, extension) = contentService.Encode(content, settings);
        var filePath = await fileService.SaveFileAsync(data, _testDirectory, extension);

        // Assert: UTF-8 with BOM.
        var bytes = File.ReadAllBytes(filePath);
        bytes.Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF });
    }

    [Fact]
    [Spec("SPEC-015-004")]
    public async Task CsvContent_EscapesSpecialCharacters()
    {
        // Arrange
        var imageLogger = _loggerFactory.CreateLogger<ImageEncodingService>();
        var contentLogger = _loggerFactory.CreateLogger<ContentEncodingService>();
        var fileLogger = _loggerFactory.CreateLogger<FileStorageService>();

        var imageService = new ImageEncodingService(imageLogger);
        var contentService = new ContentEncodingService(contentLogger, imageService);
        var fileService = new FileStorageService(fileLogger);

        var tabText = "Name\tValue\nHello, World\t\"\"\"Test\"\"\"";
        var content = new CsvContent(tabText, 2, 2);
        var settings = new SaveSettings();

        // Act
        var (data, extension) = contentService.Encode(content, settings);
        var filePath = await fileService.SaveFileAsync(data, _testDirectory, extension);

        var savedContent = File.ReadAllText(filePath, Encoding.UTF8);
        savedContent.Should().Contain("\"Hello, World\"");
        savedContent.Should().Contain("\"\"\"Test\"\"\"");
    }

    [Fact]
    [Spec("SPEC-015-001")]
    public async Task TabSeparatedText_DetectedAsCsv()
    {
        var tabText = "Name\tAge\nAlice\t30\nBob\t25";
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () => Clipboard.SetText(tabText));

        content.Should().BeOfType<CsvContent>();

        var csvContent = (CsvContent)content!;
        csvContent.RowCount.Should().Be(3);
        csvContent.ColumnCount.Should().Be(2);
    }

    [Fact]
    public async Task CsvDetection_QuotedMultilineField_DetectedAsCsv()
    {
        var tabText = "Name\tNote\nAlice\t\"Line1\nLine2\"";
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () => Clipboard.SetText(tabText));

        content.Should().BeOfType<CsvContent>();

        var csvContent = (CsvContent)content!;
        csvContent.RowCount.Should().Be(2);
        csvContent.ColumnCount.Should().Be(2);
    }

    [Fact]
    [Spec("SPEC-015-001")]
    public async Task RowWithSingleColumn_IsNotDetectedAsCsv()
    {
        var tabText = "Name\tAge\nAlice";
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () => Clipboard.SetText(tabText));

        content.Should().BeOfType<TextContent>();
    }
}

