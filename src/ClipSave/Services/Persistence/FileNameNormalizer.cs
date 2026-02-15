using ClipSave.Models;
using System.IO;

namespace ClipSave.Services;

public static class FileNamingPolicy
{
    public const string SerialOnlyFirstToken = "1";

    public static string NormalizePrefix(string? prefix)
    {
        return FileNameNormalizer.NormalizePrefix(prefix);
    }

    public static FileNamingOptions CreateOptions(SaveSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return NormalizeOptions(new FileNamingOptions
        {
            Prefix = settings.FileNamePrefix,
            IncludeTimestamp = settings.IncludeTimestamp
        });
    }

    public static FileNamingOptions NormalizeOptions(FileNamingOptions? options)
    {
        if (options == null)
        {
            return new FileNamingOptions();
        }

        return options with
        {
            Prefix = NormalizePrefix(options.Prefix)
        };
    }

    public static string BuildRawBaseName(FileNamingOptions options, string timestampToken)
    {
        if (string.IsNullOrWhiteSpace(timestampToken))
        {
            throw new ArgumentException("Timestamp token cannot be empty.", nameof(timestampToken));
        }

        var normalizedOptions = NormalizeOptions(options);
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(normalizedOptions.Prefix))
        {
            parts.Add(normalizedOptions.Prefix);
        }

        if (normalizedOptions.IncludeTimestamp)
        {
            parts.Add(timestampToken);
        }

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("_", parts);
    }

    public static string BuildPreviewFileName(
        string extension,
        string? prefix,
        bool includeTimestamp,
        string previewTimestampToken)
    {
        var normalizedExtension = NormalizePreviewExtension(extension);

        var rawBaseName = BuildRawBaseName(
            new FileNamingOptions
            {
                Prefix = prefix ?? string.Empty,
                IncludeTimestamp = includeTimestamp
            },
            previewTimestampToken);
        var normalizedBaseName = string.IsNullOrWhiteSpace(rawBaseName)
            ? SerialOnlyFirstToken
            : FileNameNormalizer.EnsureUsableBaseName(rawBaseName);

        return $"{normalizedBaseName}.{normalizedExtension}";
    }

    private static string NormalizePreviewExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return "png";
        }

        if (extension.Equals("jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return "jpg";
        }

        return extension.Trim().TrimStart('.').ToLowerInvariant();
    }
}

public static class FileNameNormalizer
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string NormalizePrefix(string? prefix)
    {
        var normalized = NormalizeSegment(prefix);
        if (normalized.Length > SaveSettings.MaxFileNamePrefixLength)
        {
            normalized = normalized[..SaveSettings.MaxFileNamePrefixLength].TrimEnd(' ', '_');
        }

        return normalized;
    }

    public static string NormalizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var chars = trimmed.Select(ch =>
            char.IsControl(ch) ||
            ch == Path.DirectorySeparatorChar ||
            ch == Path.AltDirectorySeparatorChar ||
            InvalidFileNameChars.Contains(ch)
                ? '_'
                : ch);

        var normalized = new string(chars.ToArray());
        normalized = normalized.Replace("  ", " ").Trim();

        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        normalized = normalized.Trim(' ', '.', '_');
        return normalized;
    }

    public static string EnsureUsableBaseName(string? rawBaseName)
    {
        var normalized = NormalizeSegment(rawBaseName);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = SaveSettings.DefaultFileNamePrefix;
        }

        if (IsReservedDeviceName(normalized))
        {
            normalized = "_" + normalized;
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Failed to generate a usable file name.");
        }

        return normalized;
    }

    private static bool IsReservedDeviceName(string baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return false;
        }

        // Windows treats reserved device names as invalid even when an extension is appended.
        var namePart = baseName;
        var dotIndex = namePart.IndexOf('.');
        if (dotIndex >= 0)
        {
            namePart = namePart[..dotIndex];
        }

        namePart = namePart.TrimEnd(' ', '.');
        return namePart.Length > 0 && ReservedDeviceNames.Contains(namePart);
    }
}
