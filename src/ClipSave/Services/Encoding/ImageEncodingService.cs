using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClipSave.Services;

public class ImageEncodingService
{
    private readonly ILogger<ImageEncodingService> _logger;

    public ImageEncodingService(ILogger<ImageEncodingService> logger)
    {
        _logger = logger;
    }

    public byte[] EncodeToPng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = new MemoryStream();
        encoder.Save(stream);

        _logger.LogDebug("Completed PNG encoding (Size: {Size} bytes)", stream.Length);
        return stream.ToArray();
    }

    public byte[] EncodeToJpeg(BitmapSource image, int quality = 90)
    {
        if (quality < 1 || quality > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be in the range 1 to 100.");
        }

        var processedImage = RemoveTransparency(image);

        var encoder = new JpegBitmapEncoder
        {
            QualityLevel = quality
        };
        encoder.Frames.Add(BitmapFrame.Create(processedImage));

        using var stream = new MemoryStream();
        encoder.Save(stream);

        _logger.LogDebug("Completed JPEG encoding (Quality: {Quality}, Size: {Size} bytes)",
            quality, stream.Length);
        return stream.ToArray();
    }

    private BitmapSource RemoveTransparency(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgr24 ||
            source.Format == PixelFormats.Bgr32 ||
            !HasTransparency(source.Format))
        {
            return source;
        }

        _logger.LogDebug("Converting transparent image to a white background");

        var width = source.PixelWidth;
        var height = source.PixelHeight;

        var drawingVisual = new DrawingVisual();
        using (var drawingContext = drawingVisual.RenderOpen())
        {
            drawingContext.DrawRectangle(
                System.Windows.Media.Brushes.White,
                null,
                new System.Windows.Rect(0, 0, width, height));

            drawingContext.DrawImage(source, new System.Windows.Rect(0, 0, width, height));
        }

        // Freeze is required because the encoded bitmap may be used across threads later.
        var renderTarget = new RenderTargetBitmap(
            width, height, source.DpiX, source.DpiY, PixelFormats.Pbgra32);
        renderTarget.Render(drawingVisual);
        renderTarget.Freeze();

        return renderTarget;
    }

    private static bool HasTransparency(PixelFormat format)
    {
        return format == PixelFormats.Pbgra32 ||
               format == PixelFormats.Prgba64 ||
               format == PixelFormats.Rgba64 ||
               format == PixelFormats.Bgra32 ||
               format == PixelFormats.Prgba128Float ||
               format == PixelFormats.Rgba128Float;
    }
}
