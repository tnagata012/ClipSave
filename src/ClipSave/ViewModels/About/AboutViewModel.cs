using ClipSave.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ClipSave.ViewModels.About;

public partial class AboutViewModel : ObservableObject
{
    private static readonly Regex PackageFolderVersionPattern = new(
        @"_(?<version>\d+\.\d+\.\d+\.\d+)_",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SemVerCorePattern = new(
        @"^(?<core>\d+\.\d+\.\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex InformationalVersionPattern = new(
        @"^(?<core>\d+\.\d+\.\d+(?:\.local|-[0-9A-Za-z\.-]+)?)(?:\+(?<metadata>.+))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ShaMetadataPattern = new(
        @"^sha\.(?<sha>[0-9a-fA-F]{7,40})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RawShaPattern = new(
        @"^(?<sha>[0-9a-fA-F]{7,40})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly LocalizationService _localizationService;

    public event EventHandler? RequestClose;

    public string ApplicationName { get; } = "ClipSave";
    public LocalizationService Localizer => _localizationService;

    public string Version { get; }

    public string InformationalVersion { get; }

    public string DotNetVersion { get; }

    public string OsVersion { get; }

    public string BuildDate { get; }

    public string Copyright { get; }

    public AboutViewModel()
        : this(new LocalizationService(NullLogger<LocalizationService>.Instance))
    {
    }

    public AboutViewModel(LocalizationService localizationService)
    {
        _localizationService = localizationService;
        var assembly = Assembly.GetExecutingAssembly();
        var rawInformationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        Version = GetSsoVersion(assembly, rawInformationalVersion, _localizationService);
        InformationalVersion = GetDisplayInformationalVersion(rawInformationalVersion, _localizationService);
        DotNetVersion = RuntimeInformation.FrameworkDescription;
        OsVersion = Environment.OSVersion.VersionString;
        BuildDate = GetBuildDate(assembly, _localizationService);
        Copyright = GetCopyright(assembly);
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private static string GetSsoVersion(
        Assembly assembly,
        string? rawInformationalVersion,
        LocalizationService localizationService)
    {
        if (TryExtractSemVerCore(rawInformationalVersion, out var coreVersion))
        {
            return coreVersion;
        }

        if (TryGetPackageVersionFromInstallPath(assembly.Location, out var packageVersion)
            && TryExtractSemVerCore(packageVersion, out coreVersion))
        {
            return coreVersion;
        }

        var fileVersion = assembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?
            .Version;
        if (TryExtractSemVerCore(fileVersion, out coreVersion))
        {
            return coreVersion;
        }

        var version = assembly.GetName().Version;
        if (version == null)
        {
            return localizationService.GetString("Common_Unknown");
        }

        return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }

    internal static string GetDisplayInformationalVersion(
        string? rawInformationalVersion,
        LocalizationService localizationService)
    {
        if (string.IsNullOrWhiteSpace(rawInformationalVersion))
        {
            return localizationService.GetString("Common_Unknown");
        }

        var normalized = NormalizeInformationalVersion(rawInformationalVersion);
        return string.IsNullOrWhiteSpace(normalized)
            ? localizationService.GetString("Common_Unknown")
            : normalized;
    }

    internal static string NormalizeInformationalVersion(string rawInformationalVersion)
    {
        var trimmed = rawInformationalVersion.Trim();
        var match = InformationalVersionPattern.Match(trimmed);
        if (!match.Success)
        {
            return trimmed;
        }

        var core = match.Groups["core"].Value;
        var metadata = match.Groups["metadata"].Value;
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return core;
        }

        if (TryExtractShortSha(metadata, out var shortSha))
        {
            return $"{core}+sha.{shortSha}";
        }

        return $"{core}+{metadata}";
    }

    private static bool TryExtractSemVerCore(string? value, out string coreVersion)
    {
        coreVersion = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = SemVerCorePattern.Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        coreVersion = match.Groups["core"].Value;
        return true;
    }

    private static bool TryExtractShortSha(string metadata, out string shortSha)
    {
        shortSha = string.Empty;
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return false;
        }

        var normalizedMetadata = metadata.Trim();
        var match = ShaMetadataPattern.Match(normalizedMetadata);
        if (!match.Success)
        {
            match = RawShaPattern.Match(normalizedMetadata);
        }

        if (!match.Success)
        {
            return false;
        }

        var rawSha = match.Groups["sha"].Value;
        if (string.IsNullOrWhiteSpace(rawSha))
        {
            return false;
        }

        shortSha = rawSha[..Math.Min(7, rawSha.Length)].ToLowerInvariant();
        return true;
    }

    private static bool TryGetPackageVersionFromInstallPath(string? assemblyLocation, out string version)
    {
        version = string.Empty;

        if (string.IsNullOrWhiteSpace(assemblyLocation))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(assemblyLocation);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var folderName = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                break;
            }

            var match = PackageFolderVersionPattern.Match(folderName);
            if (match.Success)
            {
                version = match.Groups["version"].Value;
                return true;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return false;
    }

    private static string GetBuildDate(Assembly assembly, LocalizationService localizationService)
    {
        try
        {
            var location = assembly.Location;
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                var lastWriteTime = File.GetLastWriteTime(location);
                return lastWriteTime.ToString("yyyy-MM-dd HH:mm");
            }

            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                var entryLocation = entryAssembly.Location;
                if (!string.IsNullOrEmpty(entryLocation) && File.Exists(entryLocation))
                {
                    var lastWriteTime = File.GetLastWriteTime(entryLocation);
                    return lastWriteTime.ToString("yyyy-MM-dd HH:mm");
                }
            }

            return localizationService.GetString("Common_Unknown");
        }
        catch
        {
            return localizationService.GetString("Common_Unknown");
        }
    }

    private static string GetCopyright(Assembly assembly)
    {
        var copyrightAttr = assembly
            .GetCustomAttribute<AssemblyCopyrightAttribute>();

        return copyrightAttr?.Copyright ?? "Copyright \u00a9 2026 TNagata012";
    }
}
