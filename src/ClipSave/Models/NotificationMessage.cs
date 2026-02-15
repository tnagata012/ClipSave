namespace ClipSave.Models;

public enum NotificationSeverity
{
    Info,
    Warning,
    Error
}

public sealed class NotificationMessage
{
    public NotificationMessage(string message, NotificationSeverity severity)
    {
        Message = message;
        Severity = severity;
    }

    public string Message { get; }
    public NotificationSeverity Severity { get; }
}
