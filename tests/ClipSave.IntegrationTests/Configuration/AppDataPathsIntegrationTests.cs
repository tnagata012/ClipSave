using ClipSave.Infrastructure;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class AppDataPathsIntegrationTests : IDisposable
{
    private readonly string _testDataRoot;
    private readonly ILoggerFactory _loggerFactory;

    public AppDataPathsIntegrationTests()
    {
        _testDataRoot = Path.Combine(Path.GetTempPath(), $"ClipSave_InstallUninstall_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataRoot);
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataRoot))
        {
            try
            {
                Directory.Delete(_testDataRoot, true);
            }
            catch
            {
                // cleanup error is ignored
            }
        }

        _loggerFactory.Dispose();
    }

    [Fact]
    [Spec("SPEC-042-001")]
    [Spec("SPEC-042-002")]
    public void AppDataRoot_IsResolvedByRuntimeMode()
    {
        var actualRoot = AppDataPaths.GetSettingsDirectory();

        if (AppDataPaths.TryGetPackageFamilyName(out var packageFamilyName))
        {
            var expectedPackagedRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages",
                packageFamilyName,
                "LocalState",
                "ClipSave");

            actualRoot.Should().Be(expectedPackagedRoot);
            return;
        }

        var expectedNonPackagedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClipSave");

        actualRoot.Should().Be(expectedNonPackagedRoot);
    }

    [Fact]
    [Spec("SPEC-042-003")]
    public void SettingsFile_IsStoredAtDataRoot()
    {
        var settingsPath = Path.Combine(_testDataRoot, "settings.json");

        _ = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _testDataRoot);

        File.Exists(settingsPath).Should().BeTrue();
    }

    [Fact]
    [Spec("SPEC-042-004")]
    [Spec("SPEC-042-005")]
    public void LogAndDumpDirectories_AreUnderDataRoot()
    {
        var dataRoot = AppDataPaths.GetSettingsDirectory();
        var logDirectory = AppDataPaths.GetLogDirectory();
        var dumpDirectory = AppDataPaths.GetDumpDirectory();

        logDirectory.Should().Be(Path.Combine(dataRoot, "logs"));
        dumpDirectory.Should().Be(Path.Combine(dataRoot, "dumps"));
    }

    [Fact]
    [Spec("SPEC-043-001")]
    public void WhenPackaged_DataRootUsesLocalState()
    {
        if (!AppDataPaths.TryGetPackageFamilyName(out var packageFamilyName))
        {
            return;
        }

        var dataRoot = AppDataPaths.GetSettingsDirectory();
        var expectedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            packageFamilyName,
            "LocalState",
            "ClipSave");

        dataRoot.Should().Be(expectedRoot);
    }

    [Fact]
    [Spec("SPEC-043-002")]
    public void PersistentDataPaths_AreContainedInDataRoot()
    {
        var dataRoot = AppDataPaths.GetSettingsDirectory();
        var settingsPath = Path.Combine(dataRoot, "settings.json");
        var logDirectory = AppDataPaths.GetLogDirectory();
        var dumpDirectory = AppDataPaths.GetDumpDirectory();

        settingsPath.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        logDirectory.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        dumpDirectory.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }
}

