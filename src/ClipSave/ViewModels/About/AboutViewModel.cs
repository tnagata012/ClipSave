using ClipSave.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ClipSave.ViewModels.About;

public partial class AboutViewModel : ObservableObject
{
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
        var version = assembly.GetName().Version;
        if (version == null)
        {
            return localizationService.GetString("Common_Unknown");
        }

        return $"{version.Major}.{version.Minor}.{version.Build}";
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
