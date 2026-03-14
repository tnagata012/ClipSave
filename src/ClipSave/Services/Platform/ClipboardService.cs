using ClipSave.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace ClipSave.Services;

public class ClipboardService
{
    private readonly ILogger<ClipboardService> _logger;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 50;
    private const int TotalTimeoutMs = 300;
    private const int ClipbrdECantOpen = unchecked((int)0x800401D0);

    public ClipboardService(ILogger<ClipboardService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClipboardContent?> GetContentAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var payload = GetClipboardPayloadInternal();
                var content = payload == null
                    ? null
                    : await CreateClipboardContentAsync(payload).ConfigureAwait(true);

                if (content != null)
                {
                    var elapsed = stopwatch.ElapsedMilliseconds;
                    _logger.LogDebug("Acquired clipboard content: {Description} (Attempt: {Attempt}, Elapsed: {Elapsed}ms)",
                        content.GetDescription(), attempt, elapsed);
                    return content;
                }

                _logger.LogDebug("No saveable clipboard content was found (Attempt: {Attempt})", attempt);
                return null;
            }
            catch (Exception ex) when (IsClipboardLocked(ex))
            {
                var elapsed = stopwatch.ElapsedMilliseconds;

                if (elapsed >= TotalTimeoutMs || attempt >= MaxRetries)
                {
                    _logger.LogWarning("Clipboard is locked and retry limit was reached (Attempt: {Attempt})",
                        attempt);
                    throw new InvalidOperationException("Failed to access clipboard after retry limit.", ex);
                }

                _logger.LogDebug("Clipboard is locked; retrying (Attempt: {Attempt})",
                    attempt);

                await Task.Delay(RetryDelayMs);
            }
        }

        return null;
    }

    public async Task<BitmapSource?> GetImageAsync()
    {
        var content = await GetContentAsync();
        return content is ImageContent imageContent ? imageContent.Image : null;
    }

    private ClipboardPayload? GetClipboardPayloadInternal()
    {
        EnsureStaThread();

        var image = GetImageInternal();
        if (image != null)
        {
            return ClipboardPayload.FromImage(image);
        }

        if (System.Windows.Clipboard.ContainsText())
        {
            var text = System.Windows.Clipboard.GetText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return ClipboardPayload.FromText(text);
            }
        }

        return null;
    }

    private async Task<ClipboardContent> CreateClipboardContentAsync(ClipboardPayload payload)
    {
        if (payload.Image != null)
        {
            return new ImageContent(payload.Image);
        }

        if (payload.Text == null)
        {
            throw new InvalidOperationException("Clipboard payload did not contain data.");
        }

        // Clipboard access must stay on STA, but text classification is pure CPU-bound work.
        _logger.LogDebug("Classifying text clipboard content on a background thread");
        var content = await Task.Run(() => ClipboardTextClassifier.Classify(payload.Text)).ConfigureAwait(true);
        LogDetectedTextContent(content);
        return content;
    }

    private void LogDetectedTextContent(ClipboardContent content)
    {
        switch (content)
        {
            case CsvContent:
                _logger.LogDebug("Detected as tabular text/CSV");
                break;
            case JsonContent:
                _logger.LogDebug("Detected as JSON");
                break;
            case MarkdownContent:
                _logger.LogDebug("Detected as Markdown");
                break;
            case TextContent:
                _logger.LogDebug("Detected as plain text");
                break;
        }
    }

    private static void EnsureStaThread()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("ClipboardService must run on an STA thread.");
        }
    }

    private BitmapSource? GetImageInternal()
    {
        EnsureStaThread();

        var pngImage = TryGetPngImage();
        if (pngImage != null)
        {
            return pngImage;
        }

        if (!System.Windows.Clipboard.ContainsImage())
        {
            return null;
        }

        var image = System.Windows.Clipboard.GetImage();
        if (image != null)
        {
            _logger.LogDebug("Retrieved image via standard clipboard format");
            return FreezeIfPossible(image);
        }

        return null;
    }

    public bool HasContent()
    {
        try
        {
            return System.Windows.Clipboard.ContainsImage() ||
                   System.Windows.Clipboard.ContainsText();
        }
        catch (Exception ex) when (IsClipboardLocked(ex))
        {
            _logger.LogDebug("Clipboard is locked (content check)");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check clipboard content");
            return false;
        }
    }

    public bool HasImage()
    {
        try
        {
            return System.Windows.Clipboard.ContainsImage();
        }
        catch (Exception ex) when (IsClipboardLocked(ex))
        {
            _logger.LogDebug("Clipboard is locked (image check)");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check clipboard image");
            return false;
        }
    }

    private BitmapSource? TryGetPngImage()
    {
        if (!System.Windows.Clipboard.ContainsData("PNG"))
        {
            return null;
        }

        try
        {
            var data = System.Windows.Clipboard.GetData("PNG");
            if (data == null)
            {
                return null;
            }

            using var stream = new MemoryStream();
            switch (data)
            {
                case MemoryStream memoryStream:
                    if (memoryStream.CanSeek)
                    {
                        memoryStream.Position = 0;
                    }
                    memoryStream.CopyTo(stream);
                    break;
                case byte[] bytes:
                    stream.Write(bytes, 0, bytes.Length);
                    break;
                case Stream dataStream:
                    if (dataStream.CanSeek)
                    {
                        dataStream.Position = 0;
                    }
                    dataStream.CopyTo(stream);
                    break;
                default:
                    _logger.LogDebug("Unsupported PNG clipboard payload type: {Type}", data.GetType().FullName);
                    return null;
            }

            stream.Position = 0;
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count > 0)
            {
                _logger.LogDebug("Retrieved image in PNG format");
                return FreezeIfPossible(decoder.Frames[0]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve PNG format; retrying with standard format");
        }

        return null;
    }

    private BitmapSource FreezeIfPossible(BitmapSource source)
    {
        if (source.CanFreeze)
        {
            try
            {
                source.Freeze();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to freeze BitmapSource");
            }
        }

        return source;
    }

    private static bool IsClipboardLocked(Exception ex)
    {
        return ex is ExternalException && ex.HResult == ClipbrdECantOpen;
    }

    private sealed record ClipboardPayload(BitmapSource? Image, string? Text)
    {
        public static ClipboardPayload FromImage(BitmapSource image)
        {
            return new ClipboardPayload(image, null);
        }

        public static ClipboardPayload FromText(string text)
        {
            return new ClipboardPayload(null, text);
        }
    }
}
