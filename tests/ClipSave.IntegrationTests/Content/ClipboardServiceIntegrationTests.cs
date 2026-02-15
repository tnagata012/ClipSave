using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class ClipboardServiceIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILoggerFactory _loggerFactory;

    public ClipboardServiceIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_ContentDetect_{Guid.NewGuid()}");
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
    [Spec("SPEC-016-001")]
    public async Task ImageDetectedWhenPresent()
    {
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () =>
            {
                var bitmap = CreateTestBitmap(64, 64);
                var data = new DataObject();
                data.SetImage(bitmap);
                Clipboard.SetDataObject(data, true);
            });

        content.Should().BeOfType<ImageContent>();
    }

    [Fact]
    [Spec("SPEC-016-004")]
    public async Task ImageAndText_OnlyImageIsSelectedAndSaved()
    {
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () =>
            {
                var bitmap = CreateTestBitmap(100, 100);
                var data = new DataObject();
                data.SetImage(bitmap);
                data.SetText("ignored text");
                Clipboard.SetDataObject(data, true);
            });

        content.Should().BeOfType<ImageContent>();

        var imageContent = (ImageContent)content!;
        var imageService = new ImageEncodingService(_loggerFactory.CreateLogger<ImageEncodingService>());
        var pngData = imageService.EncodeToPng(imageContent.Image);
        pngData.Should().NotBeEmpty();

        var fileService = new FileStorageService(_loggerFactory.CreateLogger<FileStorageService>());
        var savedPath = await fileService.SaveFileAsync(pngData, _testDirectory, "png");

        File.Exists(savedPath).Should().BeTrue();
        savedPath.Should().EndWith(".png");
    }

    [Fact]
    [Spec("SPEC-016-002")]
    public async Task TextDetection_PrefersCsvOverMarkdown()
    {
        var tabTextWithMarkdown = "# Title\tValue\nRow\t1";
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () => Clipboard.SetText(tabTextWithMarkdown));

        content.Should().BeOfType<CsvContent>();
    }

    [Fact]
    [Spec("SPEC-016-006")]
    public async Task PlainText_DetectedAsText()
    {
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () => Clipboard.SetText("Just plain text."));

        content.Should().BeOfType<TextContent>();
    }

    [Fact]
    [Spec("SPEC-016-005")]
    public async Task WhitespaceOnlyText_IsIgnored()
    {
        var content = await ClipboardTestHelper.GetContentAsync(
            _loggerFactory.CreateLogger<ClipboardService>(),
            () => Clipboard.SetText(" \t\r\n "));

        content.Should().BeNull();
    }

    [Fact]
    [Spec("SPEC-016-003")]
    public void ContentTypeSettings_CanDisableIndividualTypes()
    {
        // Arrange
        var settings = new SaveSettings
        {
            ImageEnabled = true,
            TextEnabled = false,
            MarkdownEnabled = true,
            JsonEnabled = false,
            CsvEnabled = false
        };

        // Assert
        settings.IsContentTypeEnabled(ContentType.Image).Should().BeTrue();
        settings.IsContentTypeEnabled(ContentType.Text).Should().BeFalse();
        settings.IsContentTypeEnabled(ContentType.Markdown).Should().BeTrue();
        settings.IsContentTypeEnabled(ContentType.Json).Should().BeFalse();
        settings.IsContentTypeEnabled(ContentType.Csv).Should().BeFalse();
    }

    private static BitmapSource CreateTestBitmap(int width, int height)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        var pixels = new byte[width * height * 4];
        var random = new Random(42);

        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = (byte)random.Next(256);     // B
            pixels[i + 1] = (byte)random.Next(256); // G
            pixels[i + 2] = (byte)random.Next(256); // R
            pixels[i + 3] = 255;                    // A
        }

        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        return bitmap;
    }
}

