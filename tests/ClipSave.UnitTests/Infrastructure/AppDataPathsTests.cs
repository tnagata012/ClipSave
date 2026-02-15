using ClipSave.Infrastructure;
using FluentAssertions;
using System.IO;

namespace ClipSave.UnitTests;

public class AppDataPathsTests
{
    [Fact]
    public void GetSettingsDirectory_ReturnsExpectedPathForCurrentRuntime()
    {
        var settingsDirectory = AppDataPaths.GetSettingsDirectory();

        if (AppDataPaths.TryGetPackageFamilyName(out var packageFamilyName))
        {
            settingsDirectory.Should().Be(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages",
                packageFamilyName,
                "LocalState",
                "ClipSave"));
            return;
        }

        settingsDirectory.Should().Be(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClipSave"));
    }

    [Fact]
    public void GetLogDirectory_IsUnderSettingsDirectory()
    {
        var settingsDirectory = AppDataPaths.GetSettingsDirectory();
        var logDirectory = AppDataPaths.GetLogDirectory();

        logDirectory.Should().Be(Path.Combine(settingsDirectory, "logs"));
    }

    [Fact]
    public void GetDumpDirectory_IsUnderSettingsDirectory()
    {
        var settingsDirectory = AppDataPaths.GetSettingsDirectory();
        var dumpDirectory = AppDataPaths.GetDumpDirectory();

        dumpDirectory.Should().Be(Path.Combine(settingsDirectory, "dumps"));
    }

    [Fact]
    public void GetSettingsFilePath_IsUnderSettingsDirectory()
    {
        var settingsDirectory = AppDataPaths.GetSettingsDirectory();
        var settingsPath = AppDataPaths.GetSettingsFilePath();

        settingsPath.Should().Be(Path.Combine(settingsDirectory, "settings.json"));
    }
}
