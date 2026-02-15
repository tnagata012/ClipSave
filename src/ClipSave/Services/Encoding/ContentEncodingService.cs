using ClipSave.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ClipSave.Services;

public class ContentEncodingService
{
    private readonly ILogger<ContentEncodingService> _logger;
    private readonly ImageEncodingService _imageEncodingService;

    public ContentEncodingService(
        ILogger<ContentEncodingService> logger,
        ImageEncodingService imageEncodingService)
    {
        _logger = logger;
        _imageEncodingService = imageEncodingService;
    }

    public (byte[] Data, string Extension) Encode(ClipboardContent content, SaveSettings settings)
    {
        return content switch
        {
            ImageContent img => EncodeImage(img, settings),
            TextContent txt => EncodeText(txt),
            MarkdownContent md => EncodeMarkdown(md),
            JsonContent json => EncodeJson(json),
            CsvContent csv => EncodeCsv(csv),
            _ => throw new InvalidOperationException($"Unsupported content type: {content.Type}")
        };
    }

    private (byte[] Data, string Extension) EncodeImage(ImageContent content, SaveSettings settings)
    {
        var format = settings.ImageFormat?.Trim();
        var isJpeg = string.Equals(format, "jpg", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(format, "jpeg", StringComparison.OrdinalIgnoreCase);

        byte[] data;
        string extension;

        if (isJpeg)
        {
            data = _imageEncodingService.EncodeToJpeg(content.Image, settings.JpgQuality);
            extension = "jpg";
        }
        else
        {
            data = _imageEncodingService.EncodeToPng(content.Image);
            extension = "png";
        }

        _logger.LogDebug("Encoded image ({Format}, {Size} bytes)", extension.ToUpperInvariant(), data.Length);
        return (data, extension);
    }

    private (byte[] Data, string Extension) EncodeText(TextContent content)
    {
        var data = Encoding.UTF8.GetBytes(content.Text);
        _logger.LogDebug("Encoded text (UTF-8, {Size} bytes)", data.Length);
        return (data, "txt");
    }

    private (byte[] Data, string Extension) EncodeMarkdown(MarkdownContent content)
    {
        var data = Encoding.UTF8.GetBytes(content.Text);
        _logger.LogDebug("Encoded Markdown (UTF-8, {Size} bytes)", data.Length);
        return (data, "md");
    }

    private (byte[] Data, string Extension) EncodeJson(JsonContent content)
    {
        var data = Encoding.UTF8.GetBytes(content.FormattedJson);
        _logger.LogDebug("Encoded JSON (UTF-8, {Size} bytes)", data.Length);
        return (data, "json");
    }

    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly byte[] Utf8BomPreamble = Utf8Bom.GetPreamble();

    private (byte[] Data, string Extension) EncodeCsv(CsvContent content)
    {
        var csv = ConvertTabToCsv(content.TabSeparatedText);
        // BOM is kept for compatibility with spreadsheet tools.
        var textBytes = Utf8Bom.GetBytes(csv);
        var data = new byte[Utf8BomPreamble.Length + textBytes.Length];
        Buffer.BlockCopy(Utf8BomPreamble, 0, data, 0, Utf8BomPreamble.Length);
        Buffer.BlockCopy(textBytes, 0, data, Utf8BomPreamble.Length, textBytes.Length);

        _logger.LogDebug("Encoded CSV (UTF-8 with BOM, {Size} bytes)", data.Length);
        return (data, "csv");
    }

    private static string ConvertTabToCsv(string tabText) =>
        DelimitedTextCodec.ConvertTabSeparatedToCsv(tabText);
}
