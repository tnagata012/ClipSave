using System.Windows.Media.Imaging;

namespace ClipSave.Models;

public enum ContentType
{
    Image,
    Text,
    Markdown,
    Json,
    Csv,
    None
}

public abstract record ClipboardContent(ContentType Type)
{
    public abstract string GetDescription();
}

public sealed record ImageContent(BitmapSource Image) : ClipboardContent(ContentType.Image)
{
    public override string GetDescription() =>
        $"Image ({Image.PixelWidth}x{Image.PixelHeight}, {Image.Format})";
}

public sealed record TextContent(string Text) : ClipboardContent(ContentType.Text)
{
    public override string GetDescription() =>
        $"Text ({Text.Length} chars)";
}

public sealed record MarkdownContent(string Text) : ClipboardContent(ContentType.Markdown)
{
    public override string GetDescription() =>
        $"Markdown ({Text.Length} chars)";
}

public sealed record JsonContent(string RawJson, string FormattedJson) : ClipboardContent(ContentType.Json)
{
    public override string GetDescription() =>
        $"JSON ({FormattedJson.Length} chars)";
}

public sealed record CsvContent(string TabSeparatedText, int RowCount, int ColumnCount) : ClipboardContent(ContentType.Csv)
{
    public override string GetDescription() =>
        $"Table ({RowCount} rows x {ColumnCount} cols)";
}
