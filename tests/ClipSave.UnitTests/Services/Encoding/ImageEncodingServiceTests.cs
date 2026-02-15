using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using System.Windows.Media.Imaging;

namespace ClipSave.UnitTests;

[UnitTest]
public class ImageEncodingServiceTests
{
    [Fact]
    public void EncodeToPng_ReturnsPngSignature()
    {
        var logger = Mock.Of<ILogger<ImageEncodingService>>();
        var service = new ImageEncodingService(logger);
        var bitmap = CreateTestBitmap(10, 10, hasAlpha: false);

        var encoded = service.EncodeToPng(bitmap);

        encoded.Should().NotBeEmpty();
        encoded.Take(4).Should().Equal(0x89, 0x50, 0x4E, 0x47);
    }

    [Fact]
    public void EncodeToJpeg_ReturnsJpegSignature()
    {
        var logger = Mock.Of<ILogger<ImageEncodingService>>();
        var service = new ImageEncodingService(logger);
        var bitmap = CreateTestBitmap(10, 10, hasAlpha: false);

        var encoded = service.EncodeToJpeg(bitmap, 90);

        encoded.Should().NotBeEmpty();
        encoded.Take(3).Should().Equal(0xFF, 0xD8, 0xFF);
    }

    [Fact]
    public void EncodeToJpeg_TransparentImage_UsesOpaquePixelFormat()
    {
        var logger = Mock.Of<ILogger<ImageEncodingService>>();
        var service = new ImageEncodingService(logger);
        var transparentBitmap = CreateTestBitmap(10, 10, hasAlpha: true);

        var encoded = service.EncodeToJpeg(transparentBitmap, 90);

        using var stream = new MemoryStream(encoded);
        var decoder = new JpegBitmapDecoder(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.Default);
        var frame = decoder.Frames[0];

        encoded.Take(3).Should().Equal(0xFF, 0xD8, 0xFF);
        frame.Format.ToString().Should().Match(format => format.Contains("Bgr"));
    }

    [Fact]
    public void EncodeToJpeg_CmykImage_DoesNotLogTransparencyConversion()
    {
        var logger = new Mock<ILogger<ImageEncodingService>>();
        var service = new ImageEncodingService(logger.Object);
        const int width = 4;
        const int height = 4;
        const int stride = width * 4;
        var pixels = new byte[stride * height];
        Array.Fill(pixels, (byte)128);
        var cmykBitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            System.Windows.Media.PixelFormats.Cmyk32,
            null,
            pixels,
            stride);

        var encoded = service.EncodeToJpeg(cmykBitmap, 90);

        encoded.Should().NotBeEmpty();
        encoded.Take(3).Should().Equal(0xFF, 0xD8, 0xFF);
        logger.Verify(
            value => value.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("透過画像を白背景に変換します")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void EncodeToJpeg_DifferentQuality_ProducesValidJpegData()
    {
        var logger = Mock.Of<ILogger<ImageEncodingService>>();
        var service = new ImageEncodingService(logger);
        var bitmap = CreateNoiseBitmap(100, 100);

        var quality90 = service.EncodeToJpeg(bitmap, 90);
        var quality50 = service.EncodeToJpeg(bitmap, 50);

        quality90.Should().NotBeEmpty();
        quality50.Should().NotBeEmpty();
        quality90.Take(3).Should().Equal(0xFF, 0xD8, 0xFF);
        quality50.Take(3).Should().Equal(0xFF, 0xD8, 0xFF);
    }

    private static WriteableBitmap CreateTestBitmap(int width, int height, bool hasAlpha)
    {
        var format = hasAlpha
            ? System.Windows.Media.PixelFormats.Pbgra32
            : System.Windows.Media.PixelFormats.Bgr32;
        var bitmap = new WriteableBitmap(width, height, 96, 96, format, null);
        var stride = (width * format.BitsPerPixel + 7) / 8;
        var pixels = new byte[stride * height];

        if (hasAlpha)
        {
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0;
                pixels[i + 1] = 0;
                pixels[i + 2] = 0;
                pixels[i + 3] = 0;
            }
        }
        else
        {
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
                pixels[i + 1] = 255;
                pixels[i + 2] = 255;
                pixels[i + 3] = 255;
            }
        }

        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, stride, 0);
        return bitmap;
    }

    private static WriteableBitmap CreateNoiseBitmap(int width, int height)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
        var random = new Random(42);
        var pixels = new byte[width * height * 4];
        random.NextBytes(pixels);
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        return bitmap;
    }
}
