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

    private readonly LocalizationService _localizationService;

    public event EventHandler? RequestClose;

    public string ApplicationName { get; } = "ClipSave";
    public LocalizationService Localizer => _localizationService;

    public string Version { get; }

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

        Version = GetAssemblyVersion(assembly, _localizationService);
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

    private static string GetAssemblyVersion(Assembly assembly, LocalizationService localizationService)
    {
        if (TryGetPackageVersionFromInstallPath(assembly.Location, out var packageVersion))
        {
            return packageVersion;
        }

        var version = assembly.GetName().Version;
        if (version == null)
        {
            return localizationService.GetString("Common_Unknown");
        }

        return NormalizeFourPartVersion(version);
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

    private static string NormalizeFourPartVersion(Version version)
    {
        var build = version.Build >= 0 ? version.Build : 0;
        var revision = version.Revision >= 0 ? version.Revision : 0;
        return $"{version.Major}.{version.Minor}.{build}.{revision}";
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
