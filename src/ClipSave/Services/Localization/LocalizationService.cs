using ClipSave.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace ClipSave.Services;

public class LocalizationService : INotifyPropertyChanged
{
    private static readonly ResourceManager ResourceManager =
        new("ClipSave.Resources.Strings", typeof(LocalizationService).Assembly);
    private static readonly CultureInfo FallbackCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly ILogger<LocalizationService> _logger;
    private string _currentLanguage = AppLanguage.English;
    private CultureInfo _resourceCulture = FallbackCulture;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public string CurrentLanguage => _currentLanguage;

    public string this[string key] => GetString(key);

    public LocalizationService()
        : this(NullLogger<LocalizationService>.Instance)
    {
    }

    public LocalizationService(ILogger<LocalizationService> logger)
    {
        _logger = logger;
        var initialLanguage = AppLanguage.ResolveFromSystem();
        _currentLanguage = initialLanguage;
        ApplyLanguage(initialLanguage);
    }

    public bool SetLanguage(string? languageCode)
    {
        var normalized = AppLanguage.Normalize(languageCode);
        ApplyLanguage(normalized);

        if (string.Equals(_currentLanguage, normalized, StringComparison.Ordinal))
        {
            // Re-evaluate bound strings even when the language code is unchanged.
            OnPropertyChanged("Item[]");
            return false;
        }

        _currentLanguage = normalized;
        _logger.LogInformation("Applied UI language: {Language}", normalized);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(CurrentLanguage));
        return true;
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var localized = ResourceManager.GetString(key, _resourceCulture);
        if (!string.IsNullOrEmpty(localized))
        {
            return localized;
        }

        var fallback = ResourceManager.GetString(key, FallbackCulture);
        if (!string.IsNullOrEmpty(fallback))
        {
            return fallback;
        }

        _logger.LogWarning("Missing localization resource key: {Key}", key);
        return key;
    }

    public string Format(string key, params object[] args)
    {
        var template = GetString(key);
        return args.Length == 0
            ? template
            : string.Format(_resourceCulture, template, args);
    }

    private void ApplyLanguage(string normalizedLanguage)
    {
        var culture = AppLanguage.ToCulture(normalizedLanguage);
        _resourceCulture = culture;

        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
