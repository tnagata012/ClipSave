namespace ClipSave.Models;

public enum SaveResultKind
{
    Error = 0,
    Success = 1,
    NoContent = 2,
    UnsupportedWindow = 3,
    ContentTypeDisabled = 4,
    Busy = 5
}

public class SaveResult
{
    public bool Success => Kind == SaveResultKind.Success;
    public SaveResultKind Kind { get; init; } = SaveResultKind.Error;
    public string? FilePath { get; init; }
    public string? ErrorMessage { get; init; }
    public ContentType? ContentType { get; init; }

    public static SaveResult CreateSuccess(string filePath, ContentType contentType) =>
        new() { Kind = SaveResultKind.Success, FilePath = filePath, ContentType = contentType };

    public static SaveResult CreateFailure(string errorMessage) =>
        new() { Kind = SaveResultKind.Error, ErrorMessage = errorMessage };

    public static SaveResult CreateNoContent() =>
        new() { Kind = SaveResultKind.NoContent, ErrorMessage = "No saveable clipboard content was found." };

    public static SaveResult CreateUnsupportedWindow() =>
        new() { Kind = SaveResultKind.UnsupportedWindow };

    public static SaveResult CreateContentTypeDisabled(ContentType contentType) =>
        new()
        {
            Kind = SaveResultKind.ContentTypeDisabled,
            ContentType = contentType,
            ErrorMessage = $"Saving {GetContentTypeName(contentType)} is disabled."
        };

    public static SaveResult CreateBusy() =>
        new()
        {
            Kind = SaveResultKind.Busy,
            ErrorMessage = "A save operation is already in progress."
        };

    private static string GetContentTypeName(ContentType type) => type switch
    {
        Models.ContentType.Image => "image",
        Models.ContentType.Text => "text",
        Models.ContentType.Markdown => "Markdown",
        Models.ContentType.Json => "JSON",
        Models.ContentType.Csv => "CSV",
        _ => "content"
    };
}
