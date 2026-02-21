using ClipSave.Infrastructure;
using ClipSave.Models;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ClipSave.Services;

public class CrashDumpService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string GetDumpDirectory() => AppDataPaths.GetDumpDirectory();

    public static string? WriteDump(Exception exception, string source, AppSettings? settings = null)
    {
        return WriteDump(exception, source, settings, static () => DateTime.Now);
    }

    internal static string? WriteDump(
        Exception exception,
        string source,
        AppSettings? settings,
        Func<DateTime> nowProvider)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(exception);
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(nowProvider);

            var dumpDirectory = GetDumpDirectory();
            Directory.CreateDirectory(dumpDirectory);

            var timestamp = nowProvider();
            var fileName = $"crash-{timestamp:yyyyMMdd-HHmmss-fff}.txt";
            var filePath = Path.Combine(dumpDirectory, fileName);

            var content = BuildDumpContent(exception, source, timestamp, settings);
            File.WriteAllText(filePath, content, Encoding.UTF8);

            return filePath;
        }
        catch
        {
            // Dump creation must never crash the crash handler.
            return null;
        }
    }

    internal static string BuildDumpContent(
        Exception exception,
        string source,
        DateTime timestamp,
        AppSettings? settings)
    {
        var sb = new StringBuilder();

        sb.AppendLine("================================================================================");
        sb.AppendLine("                            ClipSave Crash Dump");
        sb.AppendLine("================================================================================");
        sb.AppendLine();

        sb.AppendLine("[Basic Info]");
        sb.AppendLine($"Timestamp: {timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Exception Source: {source}");
        sb.AppendLine();

        sb.AppendLine("[Application Info]");
        sb.AppendLine($"Version: {GetAppVersion()}");
        sb.AppendLine($".NET Version: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine();

        sb.AppendLine("[Memory Info]");
        using (var process = Process.GetCurrentProcess())
        {
            sb.AppendLine($"Working Set: {FormatBytes(process.WorkingSet64)}");
            sb.AppendLine($"Private Memory: {FormatBytes(process.PrivateMemorySize64)}");
            sb.AppendLine($"GC Heap Size: {FormatBytes(GC.GetTotalMemory(false))}");
        }
        sb.AppendLine();

        sb.AppendLine("[Exception Details]");
        AppendExceptionDetails(sb, exception, 0);
        sb.AppendLine();

        if (settings != null)
        {
            sb.AppendLine("[Settings Snapshot]");
            AppendSettingsInfo(sb, settings);
            sb.AppendLine();
        }

        sb.AppendLine("================================================================================");
        sb.AppendLine("                               End of Dump");
        sb.AppendLine("================================================================================");

        return sb.ToString();
    }

    private static void AppendExceptionDetails(StringBuilder sb, Exception exception, int depth)
    {
        var indent = new string(' ', depth * 2);

        sb.AppendLine($"{indent}Type: {exception.GetType().FullName}");
        sb.AppendLine($"{indent}Message: {exception.Message}");

        if (!string.IsNullOrEmpty(exception.Source))
        {
            sb.AppendLine($"{indent}Source: {exception.Source}");
        }

        if (exception.TargetSite != null)
        {
            sb.AppendLine($"{indent}Target Method: {exception.TargetSite.DeclaringType?.FullName}.{exception.TargetSite.Name}");
        }

        if (!string.IsNullOrEmpty(exception.StackTrace))
        {
            sb.AppendLine($"{indent}Stack Trace:");
            foreach (var line in exception.StackTrace.Split('\n'))
            {
                sb.AppendLine($"{indent}  {line.Trim()}");
            }
        }

        if (exception is AggregateException aggregateException)
        {
            for (int i = 0; i < aggregateException.InnerExceptions.Count; i++)
            {
                sb.AppendLine();
                sb.AppendLine($"{indent}[Aggregate Exception #{i + 1}]");
                AppendExceptionDetails(sb, aggregateException.InnerExceptions[i], depth + 1);
            }
            return;
        }

        if (exception.InnerException != null)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}[Inner Exception (Depth: {depth + 1})]");
            AppendExceptionDetails(sb, exception.InnerException, depth + 1);
        }
    }

    private static void AppendSettingsInfo(StringBuilder sb, AppSettings settings)
    {
        var safeSettings = new
        {
            Version = settings.Version,
            Save = new
            {
                settings.Save?.ImageEnabled,
                settings.Save?.TextEnabled,
                settings.Save?.MarkdownEnabled,
                settings.Save?.JsonEnabled,
                settings.Save?.CsvEnabled,
                settings.Save?.ImageFormat,
                settings.Save?.JpgQuality
            },
            Hotkey = new
            {
                settings.Hotkey?.Modifiers,
                settings.Hotkey?.Key
            },
            Notification = new
            {
                settings.Notification?.OnSuccess,
                settings.Notification?.OnNoContent,
                settings.Notification?.OnError
            },
            Advanced = new
            {
                settings.Advanced?.Logging
            }
        };

        var json = JsonSerializer.Serialize(safeSettings, JsonOptions);
        sb.AppendLine(json);
    }

    private static string GetAppVersion()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F2} {suffixes[suffixIndex]}";
    }

}
