using ClipSave.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace ClipSave.Services;

public partial class ClipboardService
{
    private readonly ILogger<ClipboardService> _logger;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 50;
    private const int TotalTimeoutMs = 300;
    private const int ClipbrdECantOpen = unchecked((int)0x800401D0);

    // Heuristic matcher used for lightweight Markdown detection.
    [GeneratedRegex(@"^(#{1,6}\s|[-*+]\s|\d+\.\s|```|>\s|\[.+\]\(.+\)|\*\*.+\*\*|__.+__)", RegexOptions.Multiline)]
    private static partial Regex MarkdownRegex();

    public ClipboardService(ILogger<ClipboardService> logger)
    {
        _logger = logger;
    }

    public async Task<ClipboardContent?> GetContentAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var content = GetContentInternal();
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

    private ClipboardContent? GetContentInternal()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("ClipboardService must run on an STA thread.");
        }

        var image = GetImageInternal();
        if (image != null)
        {
            return new ImageContent(image);
        }

        if (System.Windows.Clipboard.ContainsText())
        {
            var text = System.Windows.Clipboard.GetText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var csvContent = TryParseAsCsv(text);
                if (csvContent != null)
                {
                    _logger.LogDebug("Detected as tabular text/CSV");
                    return csvContent;
                }

                var jsonContent = TryParseAsJson(text);
                if (jsonContent != null)
                {
                    _logger.LogDebug("Detected as JSON");
                    return jsonContent;
                }

                if (IsMarkdown(text))
                {
                    _logger.LogDebug("Detected as Markdown");
                    return new MarkdownContent(text);
                }

                _logger.LogDebug("Detected as plain text");
                return new TextContent(text);
            }
        }

        return null;
    }

    private bool IsMarkdown(string text)
    {
        return MarkdownRegex().IsMatch(text);
    }

    private JsonContent? TryParseAsJson(string text)
    {
        var trimmed = text.Trim();

        if (!(trimmed.StartsWith('{') || trimmed.StartsWith('[')))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var formatted = JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            return new JsonContent(trimmed, formatted);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private CsvContent? TryParseAsCsv(string text)
    {
        if (!text.Contains('\t'))
        {
            return null;
        }

        if (!DelimitedTextCodec.TryParseTabSeparated(text, out var rows))
        {
            return null;
        }

        // Keep the classifier stable even when clipboard text contains trailing blank lines.
        var effectiveRows = rows
            .Where(r => r.Count > 1 || r.Any(cell => !string.IsNullOrEmpty(cell)))
            .ToList();

        if (effectiveRows.Count < 2)
        {
            return null;
        }

        if (effectiveRows.Any(r => r.Count < 2))
        {
            return null;
        }

        var columnCounts = effectiveRows.Select(r => r.Count).ToList();
        var maxColumns = columnCounts.Max();

        return new CsvContent(text, effectiveRows.Count, maxColumns);
    }

    private BitmapSource? GetImageInternal()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("ClipboardService must run on an STA thread.");
        }

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
}
