namespace ClipSave.Models;

public class SettingsCorruptionEventArgs : EventArgs
{
    public string? BackupPath { get; }
    public Exception? Exception { get; }

    public SettingsCorruptionEventArgs(string? backupPath, Exception? exception = null)
    {
        BackupPath = backupPath;
        Exception = exception;
    }
}
