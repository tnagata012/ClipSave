using ClipSave.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;

namespace ClipSave.ViewModels.Settings;

// Keep designer data isolated from real user settings so previewing never mutates app state.
public sealed class SettingsDesignViewModel : SettingsViewModel
{
    private static readonly string DesignerSettingsDirectory =
        Path.Combine(Path.GetTempPath(), "ClipSave", "Designer");

    public SettingsDesignViewModel()
        : base(
            new SettingsService(NullLogger<SettingsService>.Instance, DesignerSettingsDirectory),
            new LocalizationService(NullLogger<LocalizationService>.Instance),
            NullLogger<SettingsViewModel>.Instance)
    {
    }
}
