using ClipSave.Infrastructure;
using ClipSave.Models;
using ClipSave.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Input;

namespace ClipSave.ViewModels.Settings;

public partial class SettingsViewModel : ObservableObject
{
    private const string PreviewTimestamp = "20260210_153012";
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localizationService;
    private readonly ILogger<SettingsViewModel> _logger;
    private string? _originalSettingsFingerprint;
    private bool _isApplyingSettings;
    private static readonly JsonSerializerOptions DirtyTrackingJsonOptions = new()
    {
        WriteIndented = false
    };

    public event EventHandler<bool?>? RequestClose;

    public IReadOnlyList<string> FormatOptions { get; } = new[] { "png", "jpg" };
    public LocalizationService Localizer => _localizationService;
    public bool IsDirty => _originalSettingsFingerprint != null &&
                           !string.Equals(_originalSettingsFingerprint, ComputeCurrentSettingsFingerprint(),
                               StringComparison.Ordinal);
    public bool CanSave => IsDirty;
    public bool HasUnsavedChanges => IsDirty;
    public string CancelButtonText => IsDirty
        ? _localizationService.GetString("Common_Cancel")
        : _localizationService.GetString("Common_Close");
    public bool IsImageSettingsEnabled => ImageEnabled;
    public bool HasAnyEnabledContentType => ImageEnabled || TextEnabled || MarkdownEnabled || JsonEnabled || CsvEnabled;
    public bool HasNoEnabledContentType => !HasAnyEnabledContentType;
    public bool HasNoModifierKey => !HotkeyCtrl && !HotkeyShift && !HotkeyAlt;
    public bool IsJpegSelected => string.Equals(SaveFormat, "jpg", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(SaveFormat, "jpeg", StringComparison.OrdinalIgnoreCase);
    public int FileNamePrefixMaxLength => SaveSettings.MaxFileNamePrefixLength;
    public string FileNamePreview => BuildFileNamePreview();

    public bool HasError => !string.IsNullOrEmpty(StatusMessage);

    public IReadOnlyList<Key> HotkeyKeys { get; } = BuildHotkeyKeys();
    public IReadOnlyList<LanguageOptionItem> LanguageOptions { get; }
    public string HotkeyPreview => BuildHotkeyPreview();

    [ObservableProperty]
    private string _saveFormat = "png";

    [ObservableProperty]
    private int _jpegQuality = 90;

    [ObservableProperty]
    private string _fileNamePrefix = SaveSettings.DefaultFileNamePrefix;

    [ObservableProperty]
    private bool _includeTimestamp = true;

    [ObservableProperty]
    private bool _hotkeyCtrl = true;

    [ObservableProperty]
    private bool _hotkeyShift = true;

    [ObservableProperty]
    private bool _hotkeyAlt;

    [ObservableProperty]
    private Key _hotkeyKey = Key.V;

    [ObservableProperty]
    private bool _notifyOnSuccess;

    [ObservableProperty]
    private bool _notifyOnNoContent;

    [ObservableProperty]
    private bool _notifyOnError = true;

    [ObservableProperty]
    private bool _logging;

    [ObservableProperty]
    private string _selectedLanguage = AppLanguage.English;

    [ObservableProperty]
    private bool _imageEnabled = true;

    [ObservableProperty]
    private bool _textEnabled = true;

    [ObservableProperty]
    private bool _markdownEnabled = true;

    [ObservableProperty]
    private bool _jsonEnabled = true;

    [ObservableProperty]
    private bool _csvEnabled = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(SettingsService settingsService)
        : this(
            settingsService,
            new LocalizationService(NullLogger<LocalizationService>.Instance),
            NullLogger<SettingsViewModel>.Instance)
    {
    }

    public SettingsViewModel(SettingsService settingsService, ILogger<SettingsViewModel> logger)
        : this(
            settingsService,
            new LocalizationService(NullLogger<LocalizationService>.Instance),
            logger)
    {
    }

    public SettingsViewModel(
        SettingsService settingsService,
        LocalizationService localizationService,
        ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        _logger = logger;
        _localizationService.SetLanguage(settingsService.Current.Ui.Language);
        LanguageOptions = BuildLanguageOptions();
        LoadFromSettings(settingsService.Current);
        _originalSettingsFingerprint = ComputeCurrentSettingsFingerprint();
        PropertyChanged += OnViewModelPropertyChanged;
        NotifyDirtyStateChanged();
    }

    private void LoadFromSettings(AppSettings settings)
    {
        _isApplyingSettings = true;
        try
        {
            SaveFormat = NormalizeFormat(settings.Save.ImageFormat);
            JpegQuality = settings.Save.JpgQuality;
            FileNamePrefix = FileNamingPolicy.NormalizePrefix(settings.Save.FileNamePrefix);
            IncludeTimestamp = settings.Save.IncludeTimestamp;

            ImageEnabled = settings.Save.ImageEnabled;
            TextEnabled = settings.Save.TextEnabled;
            MarkdownEnabled = settings.Save.MarkdownEnabled;
            JsonEnabled = settings.Save.JsonEnabled;
            CsvEnabled = settings.Save.CsvEnabled;

            var modifiers = settings.Hotkey.Modifiers ?? new List<string>();
            HotkeyCtrl = modifiers.Any(m => m.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
                                            m.Equals("Ctrl", StringComparison.OrdinalIgnoreCase));
            HotkeyShift = modifiers.Any(m => m.Equals("Shift", StringComparison.OrdinalIgnoreCase));
            HotkeyAlt = modifiers.Any(m => m.Equals("Alt", StringComparison.OrdinalIgnoreCase));
            HotkeyKey = ParseKey(settings.Hotkey.Key);

            NotifyOnSuccess = settings.Notification.OnSuccess;
            NotifyOnNoContent = settings.Notification.OnNoContent;
            NotifyOnError = settings.Notification.OnError;

            Logging = settings.Advanced.Logging;
            SelectedLanguage = AppLanguage.Normalize(settings.Ui?.Language);
        }
        finally
        {
            _isApplyingSettings = false;
        }

        NotifyComputedProperties();
    }

    partial void OnSaveFormatChanged(string value)
    {
        var normalized = NormalizeFormat(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            SaveFormat = normalized;
        }
    }

    partial void OnJpegQualityChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 100);
        if (clamped != value)
        {
            JpegQuality = clamped;
        }
    }

    partial void OnFileNamePrefixChanged(string value)
    {
        var normalized = FileNamingPolicy.NormalizePrefix(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            FileNamePrefix = normalized;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        StatusMessage = string.Empty;

        if (!Validate(out var error))
        {
            StatusMessage = error;
            return;
        }

        try
        {
            var updated = BuildSettings();
            _settingsService.SaveSettings(updated);
            _originalSettingsFingerprint = ComputeCurrentSettingsFingerprint();
            NotifyDirtyStateChanged();
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            StatusMessage = _localizationService.Format("SettingsViewModel_SaveFailed", ex.Message);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        // Route through the standard close path so unsaved-change confirmation always runs.
        RequestClose?.Invoke(this, null);
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var defaults = new AppSettings();
        LoadFromSettings(defaults);
        OnSettingChanged();
    }

    private bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(SaveFormat) ||
            (!SaveFormat.Equals("png", StringComparison.OrdinalIgnoreCase) &&
             !SaveFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase) &&
             !SaveFormat.Equals("jpeg", StringComparison.OrdinalIgnoreCase)))
        {
            error = _localizationService.GetString("SettingsViewModel_InvalidFormat");
            return false;
        }

        if (IsJpegSelected && (JpegQuality < 1 || JpegQuality > 100))
        {
            error = _localizationService.GetString("SettingsViewModel_InvalidJpegQuality");
            return false;
        }

        if (!HasAnyEnabledContentType)
        {
            error = _localizationService.GetString("SettingsViewModel_NoContentType");
            return false;
        }

        if (!HotkeyCtrl && !HotkeyShift && !HotkeyAlt)
        {
            error = _localizationService.GetString("SettingsViewModel_NoModifier");
            return false;
        }

        if (HotkeyKey == Key.None)
        {
            error = _localizationService.GetString("SettingsViewModel_NoHotkeyKey");
            return false;
        }

        error = string.Empty;
        return true;
    }

    private AppSettings BuildSettings()
    {
        var current = _settingsService.Current;

        var modifiers = new List<string>();
        if (HotkeyCtrl)
        {
            modifiers.Add("Control");
        }

        if (HotkeyShift)
        {
            modifiers.Add("Shift");
        }

        if (HotkeyAlt)
        {
            modifiers.Add("Alt");
        }

        return new AppSettings
        {
            Version = current.Version,
            Save = new SaveSettings
            {
                ImageFormat = NormalizeFormat(SaveFormat),
                JpgQuality = JpegQuality,
                FileNamePrefix = FileNamingPolicy.NormalizePrefix(FileNamePrefix),
                IncludeTimestamp = IncludeTimestamp,
                ImageEnabled = ImageEnabled,
                TextEnabled = TextEnabled,
                MarkdownEnabled = MarkdownEnabled,
                JsonEnabled = JsonEnabled,
                CsvEnabled = CsvEnabled
            },
            Hotkey = new HotkeySettings
            {
                Modifiers = modifiers,
                Key = HotkeyKey.ToString()
            },
            Notification = new NotificationSettings
            {
                OnSuccess = NotifyOnSuccess,
                OnNoContent = NotifyOnNoContent,
                OnError = NotifyOnError
            },
            Advanced = new AdvancedSettings
            {
                Logging = Logging,
                // Preserve internal flags that are not exposed to the UI.
                StartupGuidanceShown = current.Advanced.StartupGuidanceShown
            },
            Ui = new UiSettings
            {
                Language = AppLanguage.Normalize(SelectedLanguage, useSystemWhenMissing: false)
            }
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var propertyName = e.PropertyName;
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        if (propertyName == nameof(StatusMessage))
        {
            OnPropertyChanged(nameof(HasError));
            return;
        }

        if (_isApplyingSettings)
        {
            return;
        }

        var isSettingChanged = false;

        switch (propertyName)
        {
            case nameof(SaveFormat):
                OnPropertyChanged(nameof(IsJpegSelected));
                OnPropertyChanged(nameof(FileNamePreview));
                isSettingChanged = true;
                break;
            case nameof(JpegQuality):
            case nameof(FileNamePrefix):
            case nameof(IncludeTimestamp):
                OnPropertyChanged(nameof(FileNamePreview));
                isSettingChanged = true;
                break;
            case nameof(NotifyOnSuccess):
            case nameof(NotifyOnNoContent):
            case nameof(NotifyOnError):
            case nameof(Logging):
            case nameof(SelectedLanguage):
                isSettingChanged = true;
                break;
            case nameof(HotkeyCtrl):
            case nameof(HotkeyShift):
            case nameof(HotkeyAlt):
                OnPropertyChanged(nameof(HasNoModifierKey));
                OnPropertyChanged(nameof(HotkeyPreview));
                isSettingChanged = true;
                break;
            case nameof(HotkeyKey):
                OnPropertyChanged(nameof(HotkeyPreview));
                isSettingChanged = true;
                break;
            case nameof(ImageEnabled):
                OnPropertyChanged(nameof(IsImageSettingsEnabled));
                goto case nameof(TextEnabled);
            case nameof(TextEnabled):
            case nameof(MarkdownEnabled):
            case nameof(JsonEnabled):
            case nameof(CsvEnabled):
                OnPropertyChanged(nameof(HasAnyEnabledContentType));
                OnPropertyChanged(nameof(HasNoEnabledContentType));
                isSettingChanged = true;
                break;
        }

        if (isSettingChanged)
        {
            OnSettingChanged();
        }
    }

    private void OnSettingChanged()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        ClearErrorIfNeeded();
        NotifyDirtyStateChanged();
    }

    private void ClearErrorIfNeeded()
    {
        if (HasError)
        {
            StatusMessage = string.Empty;
        }
    }

    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(IsJpegSelected));
        OnPropertyChanged(nameof(IsImageSettingsEnabled));
        OnPropertyChanged(nameof(HasAnyEnabledContentType));
        OnPropertyChanged(nameof(HasNoEnabledContentType));
        OnPropertyChanged(nameof(HasNoModifierKey));
        OnPropertyChanged(nameof(HotkeyPreview));
        OnPropertyChanged(nameof(FileNamePreview));
    }

    private void NotifyDirtyStateChanged()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CancelButtonText));
        SaveCommand.NotifyCanExecuteChanged();
    }

    private string BuildHotkeyPreview()
    {
        var parts = new List<string>();
        if (HotkeyCtrl)
        {
            parts.Add("Ctrl");
        }

        if (HotkeyShift)
        {
            parts.Add("Shift");
        }

        if (HotkeyAlt)
        {
            parts.Add("Alt");
        }

        if (HotkeyKey != Key.None)
        {
            parts.Add(FormatHotkeyKey(HotkeyKey));
        }

        return parts.Count == 0
            ? _localizationService.GetString("SettingsViewModel_HotkeyNotSet")
            : string.Join(" + ", parts);
    }

    private static string FormatHotkeyKey(Key key)
    {
        var text = key.ToString();
        return text.StartsWith('D') && text.Length == 2 && char.IsDigit(text[1])
            ? text[1].ToString()
            : text;
    }

    private string ComputeCurrentSettingsFingerprint()
    {
        var settings = BuildSettings();
        return JsonSerializer.Serialize(settings, DirtyTrackingJsonOptions);
    }

    private static string NormalizeFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return "png";
        }

        if (format.Equals("jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return "jpg";
        }

        return format.Trim().ToLowerInvariant();
    }

    private string BuildFileNamePreview()
    {
        return FileNamingPolicy.BuildPreviewFileName(
            SaveFormat,
            FileNamePrefix,
            IncludeTimestamp,
            PreviewTimestamp);
    }

    private static IReadOnlyList<Key> BuildHotkeyKeys()
    {
        var keys = new List<Key>();

        for (char c = 'A'; c <= 'Z'; c++)
        {
            keys.Add((Key)Enum.Parse(typeof(Key), c.ToString()));
        }

        for (int i = 0; i <= 9; i++)
        {
            keys.Add((Key)Enum.Parse(typeof(Key), $"D{i}"));
        }

        for (int i = 1; i <= 12; i++)
        {
            keys.Add((Key)Enum.Parse(typeof(Key), $"F{i}"));
        }

        return keys;
    }

    private static Key ParseKey(string key)
    {
        if (Enum.TryParse<Key>(key, ignoreCase: true, out var parsed) && parsed != Key.None)
        {
            return parsed;
        }

        return Key.V;
    }

    private IReadOnlyList<LanguageOptionItem> BuildLanguageOptions()
    {
        return new ReadOnlyCollection<LanguageOptionItem>(
            new List<LanguageOptionItem>
            {
                new(AppLanguage.English, _localizationService.GetString("SettingsViewModel_Language_English")),
                new(AppLanguage.Japanese, _localizationService.GetString("SettingsViewModel_Language_Japanese"))
            });
    }
}

public sealed record LanguageOptionItem(string Code, string DisplayName);
