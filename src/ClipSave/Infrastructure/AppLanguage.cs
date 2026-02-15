using System.Globalization;

namespace ClipSave.Infrastructure;

public static class AppLanguage
{
    public const string English = "en";
    public const string Japanese = "ja";

    public static string Normalize(string? languageCode, bool useSystemWhenMissing = true)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return useSystemWhenMissing ? ResolveFromSystem() : English;
        }

        var trimmed = languageCode.Trim();
        if (trimmed.Equals(Japanese, StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("ja-", StringComparison.OrdinalIgnoreCase))
        {
            return Japanese;
        }

        if (trimmed.Equals(English, StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
        {
            return English;
        }

        return English;
    }

    public static string ResolveFromSystem(CultureInfo? systemCulture = null)
    {
        var culture = systemCulture ?? CultureInfo.InstalledUICulture;
        return culture.TwoLetterISOLanguageName.Equals(Japanese, StringComparison.OrdinalIgnoreCase)
            ? Japanese
            : English;
    }

    public static CultureInfo ToCulture(string languageCode)
    {
        var normalized = Normalize(languageCode, useSystemWhenMissing: false);
        return normalized == Japanese
            ? CultureInfo.GetCultureInfo("ja-JP")
            : CultureInfo.GetCultureInfo("en-US");
    }
}
