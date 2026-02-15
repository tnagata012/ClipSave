using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ClipSave.Infrastructure;

internal static class AppDataPaths
{
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;
    private const int AppModelErrorNoPackage = 15700;
    private const string PackageRootDirectoryName = "Packages";
    private const string LocalStateDirectoryName = "LocalState";
    private const string LogsDirectoryName = "logs";
    private const string DumpsDirectoryName = "dumps";
    internal const string AppDirectoryName = "ClipSave";
    internal const string SettingsFileName = "settings.json";
    private static readonly Lazy<string> DataRootDirectory = new(ResolveDataRootDirectory, true);

    public static string GetSettingsDirectory()
    {
        return GetDataRootDirectory();
    }

    public static string GetDataRootDirectory()
    {
        return DataRootDirectory.Value;
    }

    public static string GetSettingsFilePath()
    {
        return Path.Combine(GetSettingsDirectory(), SettingsFileName);
    }

    public static string GetLogDirectory()
    {
        return Path.Combine(GetDataRootDirectory(), LogsDirectoryName);
    }

    public static string GetDumpDirectory()
    {
        return Path.Combine(GetDataRootDirectory(), DumpsDirectoryName);
    }

    private static string ResolveDataRootDirectory()
    {
        if (TryGetPackageFamilyName(out var packageFamilyName))
        {
            var localAppData = GetKnownFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolder.ApplicationData);

            return Path.GetFullPath(Path.Combine(
                localAppData,
                PackageRootDirectoryName,
                packageFamilyName,
                LocalStateDirectoryName,
                AppDirectoryName));
        }

        var appData = GetKnownFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolder.LocalApplicationData);

        return Path.GetFullPath(Path.Combine(appData, AppDirectoryName));
    }

    private static string GetKnownFolderPath(
        Environment.SpecialFolder primary,
        Environment.SpecialFolder fallback)
    {
        var primaryPath = Environment.GetFolderPath(primary);
        if (!string.IsNullOrWhiteSpace(primaryPath))
        {
            return primaryPath;
        }

        var fallbackPath = Environment.GetFolderPath(fallback);
        if (!string.IsNullOrWhiteSpace(fallbackPath))
        {
            return fallbackPath;
        }

        return AppContext.BaseDirectory;
    }

    internal static bool TryGetPackageFamilyName(out string packageFamilyName)
    {
        var bufferLength = 0;
        var result = GetCurrentPackageFamilyName(ref bufferLength, null);

        if (result == AppModelErrorNoPackage)
        {
            packageFamilyName = string.Empty;
            return false;
        }

        if (result != ErrorInsufficientBuffer || bufferLength <= 0)
        {
            packageFamilyName = string.Empty;
            return false;
        }

        var buffer = new StringBuilder(bufferLength);
        result = GetCurrentPackageFamilyName(ref bufferLength, buffer);

        if (result != ErrorSuccess || buffer.Length == 0)
        {
            packageFamilyName = string.Empty;
            return false;
        }

        packageFamilyName = buffer.ToString();
        return true;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(
        ref int packageFamilyNameLength,
        StringBuilder? packageFamilyName);
}
