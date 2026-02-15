using ClipSave.Models;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ClipSave.Services;

public class FileStorageService
{
    private readonly ILogger<FileStorageService> _logger;
    private const int MaxSaveAttempts = 3;
    private const int MaxDuplicateCounter = 1000;

    public FileStorageService(ILogger<FileStorageService> logger)
    {
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(byte[] data, string directory, string extension)
    {
        return await SaveFileAsync(data, directory, extension, new FileNamingOptions());
    }

    public async Task<string> SaveFileAsync(
        byte[] data,
        string directory,
        string extension,
        FileNamingOptions namingOptions)
    {
        var normalizedDirectory = NormalizeDirectoryPath(directory);
        var normalizedExtension = NormalizeExtension(extension);
        var normalizedNamingOptions = FileNamingPolicy.NormalizeOptions(namingOptions);

        Directory.CreateDirectory(normalizedDirectory);

        for (int attempt = 0; attempt < MaxSaveAttempts; attempt++)
        {
            var fileName = GenerateUniqueFileName(
                normalizedDirectory,
                normalizedExtension,
                normalizedNamingOptions);
            var fullPath = Path.Combine(normalizedDirectory, fileName);

            var tempPath = fullPath + ".tmp";
            var committed = false;

            try
            {
                await File.WriteAllBytesAsync(tempPath, data);

                File.Move(tempPath, fullPath, overwrite: false);
                committed = true;

                _logger.LogDebug("Saved file: {Path} ({Size} bytes)",
                    fullPath, data.Length);

                return fullPath;
            }
            catch (IOException) when (File.Exists(fullPath) && attempt < MaxSaveAttempts - 1)
            {
                _logger.LogDebug("Filename collision detected; retrying: {Path}", fullPath);
            }
            finally
            {
                if (!committed)
                {
                    DeleteTempFileSafely(tempPath);
                }
            }
        }

        throw new InvalidOperationException("Failed to save file after reaching retry limit.");
    }

    private void DeleteTempFileSafely(string tempPath)
    {
        if (!File.Exists(tempPath))
        {
            return;
        }

        try
        {
            File.Delete(tempPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Failed to delete temporary file: {Path}", tempPath);
        }
    }

    private string GenerateUniqueFileName(
        string directory,
        string extension,
        FileNamingOptions namingOptions)
    {
        var timestampToken = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var rawBaseName = FileNamingPolicy.BuildRawBaseName(namingOptions, timestampToken);
        var isSerialOnlyBaseName = string.IsNullOrWhiteSpace(rawBaseName);
        var normalizedBaseName = isSerialOnlyBaseName
            ? string.Empty
            : FileNameNormalizer.EnsureUsableBaseName(rawBaseName);

        for (int counter = 0; counter <= MaxDuplicateCounter; counter++)
        {
            var fileName = isSerialOnlyBaseName
                ? $"{counter + 1}.{extension}"
                : $"{normalizedBaseName}{(counter == 0 ? string.Empty : $"_{counter}")}.{extension}";
            var fullPath = Path.Combine(directory, fileName);
            if (!File.Exists(fullPath))
            {
                return fileName;
            }

            _logger.LogDebug("Filename duplicate detected; retrying with numeric suffix: {FileName}", fileName);
        }

        throw new InvalidOperationException("Failed to generate filename after reaching suffix limit.");
    }

    public bool HasEnoughSpace(string directory, long requiredBytes)
    {
        try
        {
            var normalizedPath = NormalizeDirectoryPath(directory);
            var root = Path.GetPathRoot(normalizedPath);
            if (string.IsNullOrWhiteSpace(root) ||
                root.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return true;
            }

            var drive = new DriveInfo(root);

            return drive.AvailableFreeSpace >= requiredBytes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check available disk space");
            // Keep save attempts best-effort when free-space probing is unavailable.
            return true;
        }
    }

    private static string NormalizeDirectoryPath(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Target directory cannot be empty.", nameof(directory));
        }

        var expanded = Environment.ExpandEnvironmentVariables(directory.Trim());
        if (expanded.StartsWith("::", StringComparison.Ordinal))
        {
            throw new ArgumentException("Target directory must be a file-system path.", nameof(directory));
        }

        return Path.GetFullPath(expanded);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("Extension cannot be empty.", nameof(extension));
        }

        var normalized = extension.Trim().TrimStart('.');
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Extension cannot be empty.", nameof(extension));
        }

        if (normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Extension contains invalid characters.", nameof(extension));
        }

        if (normalized.Contains(Path.DirectorySeparatorChar) ||
            normalized.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Extension cannot contain path separator characters.", nameof(extension));
        }

        return normalized.ToLowerInvariant();
    }
}
