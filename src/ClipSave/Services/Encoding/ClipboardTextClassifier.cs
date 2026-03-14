using ClipSave.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClipSave.Services;

internal static partial class ClipboardTextClassifier
{
    // Heuristic matcher used for lightweight Markdown detection.
    [GeneratedRegex(@"^(#{1,6}\s|[-*+]\s|\d+\.\s|```|>\s|\[.+\]\(.+\)|\*\*.+\*\*|__.+__)", RegexOptions.Multiline)]
    private static partial Regex MarkdownRegex();

    public static ClipboardContent Classify(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var csvContent = TryParseAsCsv(text);
        if (csvContent != null)
        {
            return csvContent;
        }

        var jsonContent = TryParseAsJson(text);
        if (jsonContent != null)
        {
            return jsonContent;
        }

        if (MarkdownRegex().IsMatch(text))
        {
            return new MarkdownContent(text);
        }

        return new TextContent(text);
    }

    private static JsonContent? TryParseAsJson(string text)
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

    private static CsvContent? TryParseAsCsv(string text)
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
}
