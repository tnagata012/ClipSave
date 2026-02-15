using System.Text.Json.Serialization;

namespace ClipSave.Models;

public class AppSettings
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("save")]
    public SaveSettings Save { get; set; } = new();

    [JsonPropertyName("hotkey")]
    public HotkeySettings Hotkey { get; set; } = new();

    [JsonPropertyName("notification")]
    public NotificationSettings Notification { get; set; } = new();

    [JsonPropertyName("advanced")]
    public AdvancedSettings Advanced { get; set; } = new();

    [JsonPropertyName("ui")]
    public UiSettings Ui { get; set; } = new();
}

public class SaveSettings
{
    public const string DefaultFileNamePrefix = "CS";
    public const int MaxFileNamePrefixLength = 16;

    [JsonPropertyName("imageEnabled")]
    public bool ImageEnabled { get; set; } = true;

    [JsonPropertyName("textEnabled")]
    public bool TextEnabled { get; set; } = true;

    [JsonPropertyName("markdownEnabled")]
    public bool MarkdownEnabled { get; set; } = true;

    [JsonPropertyName("jsonEnabled")]
    public bool JsonEnabled { get; set; } = true;

    [JsonPropertyName("csvEnabled")]
    public bool CsvEnabled { get; set; } = true;

    [JsonPropertyName("imageFormat")]
    public string ImageFormat { get; set; } = "png";

    [JsonPropertyName("jpgQuality")]
    public int JpgQuality { get; set; } = 90;

    [JsonPropertyName("fileNamePrefix")]
    public string FileNamePrefix { get; set; } = DefaultFileNamePrefix;

    [JsonPropertyName("includeTimestamp")]
    public bool IncludeTimestamp { get; set; } = true;

    [JsonIgnore]
    public bool HasAnyEnabledContentType =>
        ImageEnabled || TextEnabled || MarkdownEnabled || JsonEnabled || CsvEnabled;

    public bool IsContentTypeEnabled(ContentType contentType)
    {
        return contentType switch
        {
            ContentType.Image => ImageEnabled,
            ContentType.Text => TextEnabled,
            ContentType.Markdown => MarkdownEnabled,
            ContentType.Json => JsonEnabled,
            ContentType.Csv => CsvEnabled,
            _ => false
        };
    }
}

public sealed record FileNamingOptions
{
    public string Prefix { get; init; } = SaveSettings.DefaultFileNamePrefix;

    public bool IncludeTimestamp { get; init; } = true;
}

public class HotkeySettings
{
    [JsonPropertyName("modifiers")]
    public List<string> Modifiers { get; set; } = new() { "Control", "Shift" };

    [JsonPropertyName("key")]
    public string Key { get; set; } = "V";
}

public class NotificationSettings
{
    [JsonPropertyName("onSuccess")]
    public bool OnSuccess { get; set; } = false;

    [JsonPropertyName("onNoContent")]
    public bool OnNoContent { get; set; } = false;

    [JsonPropertyName("onError")]
    public bool OnError { get; set; } = true;
}

public class AdvancedSettings
{
    [JsonPropertyName("logging")]
    public bool Logging { get; set; } = false;

    [JsonPropertyName("startupGuidanceShown")]
    public bool StartupGuidanceShown { get; set; } = false;
}

public class UiSettings
{
    [JsonPropertyName("language")]
    public string? Language { get; set; }
}
