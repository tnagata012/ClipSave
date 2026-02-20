using ClipSave.Infrastructure;
using ClipSave.Models;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace ClipSave.Services;

public class SettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;
    private AppSettings _settings;
    private SettingsCorruptionEventArgs? _pendingCorruption;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true
    };

    public event EventHandler<AppSettings>? SettingsChanged;
    public event EventHandler<SettingsCorruptionEventArgs>? SettingsCorruptionDetected;

    public AppSettings Current => _settings;

    internal static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions(JsonOptions);
    }

    public SettingsService(ILogger<SettingsService> logger)
        : this(logger, null)
    {
    }

    internal SettingsService(ILogger<SettingsService> logger, string? settingsDirectory)
    {
        _logger = logger;

        var settingsPath = settingsDirectory == null
            ? AppDataPaths.GetSettingsFilePath()
            : Path.Combine(settingsDirectory, AppDataPaths.SettingsFileName);
        var appDataPath = Path.GetDirectoryName(settingsPath)!;

        Directory.CreateDirectory(appDataPath);

        _settingsPath = settingsPath;

        _settings = LoadSettings();
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    NormalizeSettings(settings);
                }

                if (settings != null && ValidateSettings(settings))
                {
                    _logger.LogInformation("Loaded settings file: {Path}", _settingsPath);
                    return settings;
                }

                _logger.LogWarning("Settings file is invalid. Reinitializing with defaults.");
                var backupPath = BackupCorruptedSettings();
                ReportCorruption(new SettingsCorruptionEventArgs(backupPath));
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                "Settings file is not valid JSON. Reinitializing with defaults. Path: {Path}. Reason: {Reason}",
                _settingsPath,
                ex.Message);
            var backupPath = BackupCorruptedSettings();
            ReportCorruption(new SettingsCorruptionEventArgs(backupPath, ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings file");
            var backupPath = BackupCorruptedSettings();
            ReportCorruption(new SettingsCorruptionEventArgs(backupPath, ex));
        }

        var defaultSettings = new AppSettings();
        SaveSettings(defaultSettings);
        return defaultSettings;
    }

    public void SaveSettings(AppSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentException("Settings is invalid.", nameof(settings));
        }

        NormalizeSettings(settings);

        if (!ValidateSettings(settings))
        {
            throw new ArgumentException("Settings is invalid.", nameof(settings));
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempPath = _settingsPath + ".tmp";
        var committed = false;

        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsPath, overwrite: true);
            committed = true;
        }
        finally
        {
            if (!committed)
            {
                DeleteTempFileSafely(tempPath);
            }
        }

        _settings = settings;

        _logger.LogInformation("Saved settings file: {Path}", _settingsPath);
        SettingsChanged?.Invoke(this, settings);
    }

    public void UpdateSettings(Action<AppSettings> updateAction)
    {
        var newSettings = JsonSerializer.Deserialize<AppSettings>(
            JsonSerializer.Serialize(_settings, JsonOptions), JsonOptions) ?? new AppSettings();

        updateAction(newSettings);
        SaveSettings(newSettings);
    }

    internal bool TryDequeuePendingCorruption(out SettingsCorruptionEventArgs? args)
    {
        args = _pendingCorruption;
        _pendingCorruption = null;
        return args != null;
    }

    private void ReportCorruption(SettingsCorruptionEventArgs args)
    {
        if (SettingsCorruptionDetected == null)
        {
            _pendingCorruption = args;
            return;
        }

        SettingsCorruptionDetected?.Invoke(this, args);
    }

    private bool ValidateSettings(AppSettings settings)
    {
        if (settings == null)
        {
            _logger.LogWarning("Settings is null");
            return false;
        }

        if (settings.Save == null)
        {
            _logger.LogWarning("Save settings is null");
            return false;
        }

        if (settings.Hotkey == null)
        {
            _logger.LogWarning("Hotkey settings is null");
            return false;
        }

        if (settings.Notification == null)
        {
            _logger.LogWarning("Notification settings is null");
            return false;
        }

        if (settings.Advanced == null)
        {
            _logger.LogWarning("Advanced settings is null");
            return false;
        }

        if (settings.Ui == null)
        {
            _logger.LogWarning("Ui settings is null");
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.Save.ImageFormat))
        {
            _logger.LogWarning("Image format is empty");
            return false;
        }

        var format = settings.Save.ImageFormat.Trim();
        if (!format.Equals("png", StringComparison.OrdinalIgnoreCase) &&
            !format.Equals("jpg", StringComparison.OrdinalIgnoreCase) &&
            !format.Equals("jpeg", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid image format: {Format}", settings.Save.ImageFormat);
            return false;
        }

        if (settings.Save.JpgQuality < 1 || settings.Save.JpgQuality > 100)
        {
            _logger.LogWarning("JPG quality is out of range: {Quality}", settings.Save.JpgQuality);
            return false;
        }

        if (!settings.Save.HasAnyEnabledContentType)
        {
            _logger.LogWarning("All save content types are disabled");
            return false;
        }

        if (settings.Hotkey.Modifiers == null || settings.Hotkey.Modifiers.Count == 0)
        {
            _logger.LogWarning("No hotkey modifiers were specified");
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.Hotkey.Key))
        {
            _logger.LogWarning("Hotkey key is empty");
            return false;
        }

        if (!Enum.TryParse<Key>(settings.Hotkey.Key, ignoreCase: true, out var parsedHotkey) ||
            parsedHotkey == Key.None)
        {
            _logger.LogWarning("Invalid hotkey key: {Key}", settings.Hotkey.Key);
            return false;
        }

        var allowedModifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Control",
            "Ctrl",
            "Shift",
            "Alt"
        };

        foreach (var modifier in settings.Hotkey.Modifiers)
        {
            var trimmed = modifier?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed) || !allowedModifiers.Contains(trimmed))
            {
                _logger.LogWarning("Invalid hotkey modifier: {Modifier}", modifier);
                return false;
            }
        }

        if (settings.Hotkey.Modifiers.Any(m =>
            m.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
            m.Equals("Windows", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Win key is not allowed in hotkey modifiers");
            return false;
        }

        if (!string.Equals(settings.Ui.Language, AppLanguage.English, StringComparison.Ordinal) &&
            !string.Equals(settings.Ui.Language, AppLanguage.Japanese, StringComparison.Ordinal))
        {
            _logger.LogWarning("Invalid UI language: {Language}", settings.Ui.Language);
            return false;
        }

        return true;
    }

    private static void NormalizeSettings(AppSettings settings)
    {
        if (settings.Save == null)
        {
            return;
        }

        settings.Save.FileNamePrefix = FileNamingPolicy.NormalizePrefix(settings.Save.FileNamePrefix);
        settings.Ui ??= new UiSettings();
        settings.Ui.Language = AppLanguage.Normalize(settings.Ui.Language);
    }

    private string? BackupCorruptedSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var backupPath = $"{_settingsPath}.backup.{DateTime.Now:yyyyMMddHHmmss}";
                File.Copy(_settingsPath, backupPath);
                _logger.LogInformation("Backed up corrupted settings file: {Path}", backupPath);
                return backupPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to back up settings file");
        }
        return null;
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
            _logger.LogWarning(ex, "Failed to delete temporary settings file: {Path}", tempPath);
        }
    }
}
