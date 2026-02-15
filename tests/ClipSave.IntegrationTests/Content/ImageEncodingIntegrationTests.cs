using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows.Media.Imaging;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class ImageEncodingIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILoggerFactory _loggerFactory;

    public ImageEncodingIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_ImageSave_{Guid.NewGuid()}");
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
    [Spec("SPEC-011-001")]
    public async Task ImageEncode_And_FileSave_PNG_Success()
    {
        // Arrange
        var imageService = new ImageEncodingService(_loggerFactory.CreateLogger<ImageEncodingService>());
        var fileService = new FileStorageService(_loggerFactory.CreateLogger<FileStorageService>());

        var bitmap = CreateTestBitmap(50, 50, hasAlpha: false);

        // Act
        var pngData = imageService.EncodeToPng(bitmap);
        var filePath = await fileService.SaveFileAsync(pngData, _testDirectory, "png");

        // Assert
        filePath.Should().NotBeNullOrEmpty();
        filePath.Should().EndWith(".png");
        File.Exists(filePath).Should().BeTrue();

        var fileData = await File.ReadAllBytesAsync(filePath);
        fileData.Take(4).Should().Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
    }

    [Fact]
    [Spec("SPEC-011-002")]
    public async Task ImageEncode_And_FileSave_JPG_WithQuality_Success()
    {
        // Arrange
        var imageService = new ImageEncodingService(_loggerFactory.CreateLogger<ImageEncodingService>());
        var fileService = new FileStorageService(_loggerFactory.CreateLogger<FileStorageService>());

        var bitmap = CreateTestBitmap(50, 50, hasAlpha: false);

        // Act
        var jpgData = imageService.EncodeToJpeg(bitmap, 75);
        var filePath = await fileService.SaveFileAsync(jpgData, _testDirectory, "jpg");

        // Assert
        filePath.Should().EndWith(".jpg");
        File.Exists(filePath).Should().BeTrue();

        var fileData = await File.ReadAllBytesAsync(filePath);
        fileData.Take(3).Should().Equal(new byte[] { 0xFF, 0xD8, 0xFF });
    }

    [Fact]
    [Spec("SPEC-010-001")]
    public async Task SaveCreatesMissingDirectory()
    {
        // Arrange
        var imageService = new ImageEncodingService(_loggerFactory.CreateLogger<ImageEncodingService>());
        var fileService = new FileStorageService(_loggerFactory.CreateLogger<FileStorageService>());

        var missingDirectory = Path.Combine(_testDirectory, "Missing", "Nested");
        var bitmap = CreateTestBitmap(20, 20, hasAlpha: false);
        var pngData = imageService.EncodeToPng(bitmap);

        // Act
        var filePath = await fileService.SaveFileAsync(pngData, missingDirectory, "png");

        // Assert
        Directory.Exists(missingDirectory).Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    [Spec("SPEC-010-002")]
    public async Task MultipleFileSaves_UniqueFilenames()
    {
        // Arrange
        var imageService = new ImageEncodingService(_loggerFactory.CreateLogger<ImageEncodingService>());
        var fileService = new FileStorageService(_loggerFactory.CreateLogger<FileStorageService>());

        var bitmap = CreateTestBitmap(20, 20, hasAlpha: false);
        var pngData = imageService.EncodeToPng(bitmap);

        // Act
        var filePaths = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var filePath = await fileService.SaveFileAsync(pngData, _testDirectory, "png");
            filePaths.Add(filePath);
        }

        // Assert
        filePaths.Should().AllSatisfy(p => File.Exists(p).Should().BeTrue());
        filePaths.Distinct().Should().HaveCount(5, "all file names should be unique");
    }

    [Fact]
    [Spec("SPEC-011-003")]
    public void EncodeToJpeg_TransparentImage_CompositesOnWhiteBackground()
    {
        var imageService = new ImageEncodingService(_loggerFactory.CreateLogger<ImageEncodingService>());
        var transparentBitmap = CreateTransparentBitmap(1, 1);

        var jpgData = imageService.EncodeToJpeg(transparentBitmap, 90);

        using var stream = new MemoryStream(jpgData);
        var decoder = new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var bgrFrame = frame.Format == System.Windows.Media.PixelFormats.Bgr32
            ? (BitmapSource)frame
            : new System.Windows.Media.Imaging.FormatConvertedBitmap(
                frame,
                System.Windows.Media.PixelFormats.Bgr32,
                null,
                0);

        var pixels = new byte[4];
        bgrFrame.CopyPixels(pixels, 4, 0);

        ((int)pixels[0]).Should().BeGreaterThanOrEqualTo(240); // B
        ((int)pixels[1]).Should().BeGreaterThanOrEqualTo(240); // G
        ((int)pixels[2]).Should().BeGreaterThanOrEqualTo(240); // R
    }

    private static WriteableBitmap CreateTestBitmap(int width, int height, bool hasAlpha)
    {
        var format = hasAlpha
            ? System.Windows.Media.PixelFormats.Bgra32
            : System.Windows.Media.PixelFormats.Bgr32;

        var bitmap = new WriteableBitmap(width, height, 96, 96, format, null);
        var bytesPerPixel = format.BitsPerPixel / 8;
        var pixels = new byte[width * height * bytesPerPixel];
        var random = new Random(42);

        for (int i = 0; i < pixels.Length; i += bytesPerPixel)
        {
            pixels[i] = (byte)random.Next(256);     // B
            pixels[i + 1] = (byte)random.Next(256); // G
            pixels[i + 2] = (byte)random.Next(256); // R
            if (hasAlpha && bytesPerPixel == 4)
            {
                pixels[i + 3] = (byte)random.Next(128, 256); // A
            }
        }

        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * bytesPerPixel, 0);
        bitmap.Freeze();

        return bitmap;
    }

    private static WriteableBitmap CreateTransparentBitmap(int width, int height)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 0;     // B
            pixels[i + 1] = 0; // G
            pixels[i + 2] = 0; // R
            pixels[i + 3] = 0; // A
        }

        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }
}

