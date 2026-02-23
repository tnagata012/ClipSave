using ClipSave.Infrastructure;
using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using System.Text.Json;

namespace ClipSave.UnitTests;

[UnitTest]
public class SettingsServiceTests : IDisposable
{
    private readonly string _testAppDataPath;
    private readonly string _testSettingsPath;
    private readonly string _originalAppData;

    public SettingsServiceTests()
    {
        _testAppDataPath = Path.Combine(Path.GetTempPath(), $"ClipSave_SettingsTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testAppDataPath);
        _testSettingsPath = Path.Combine(_testAppDataPath, "settings.json");

        _originalAppData = Environment.GetEnvironmentVariable("APPDATA") ?? "";
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("APPDATA", _originalAppData);

        if (Directory.Exists(_testAppDataPath))
        {
            try
            {
                Directory.Delete(_testAppDataPath, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void SettingsChange_AppliedWithoutRestart()
    {
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        var initialSettings = settingsService.Current;
        initialSettings.Save.ImageFormat.Should().Be("png");

        bool settingsChangedEventFired = false;
        settingsService.SettingsChanged += (sender, settings) =>
        {
            settingsChangedEventFired = true;
        };

        settingsService.UpdateSettings(settings =>
        {
            settings.Save.ImageFormat = "jpg";
            settings.Save.JpgQuality = 85;
        });

        settingsService.Current.Save.ImageFormat.Should().Be("jpg");
        settingsService.Current.Save.JpgQuality.Should().Be(85);
        settingsChangedEventFired.Should().BeTrue();

        var savedJson = File.ReadAllText(_testSettingsPath);
        savedJson.Should().Contain("\"imageFormat\": \"jpg\"");
        savedJson.Should().Contain("\"jpgQuality\": 85");
    }

    [Fact]
    public void CorruptedSettingsFile_LoadsDefaultAndNotifies()
    {
        File.WriteAllText(_testSettingsPath, "{ invalid json content ///");

        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.Current.Should().NotBeNull();
        settingsService.Current.Save.ImageFormat.Should().Be("png");
        settingsService.Current.Save.JpgQuality.Should().Be(90);

        var backupFiles = Directory.GetFiles(_testAppDataPath, "settings.json.backup.*");
        backupFiles.Should().NotBeEmpty();

        mockLogger.Verify(
            value => value.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Settings file is not valid JSON. Reinitializing with defaults.")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        mockLogger.Verify(
            value => value.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Failed to load settings file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void SettingsWithInvalidValues_LoadsDefault()
    {
        var invalidSettings = new AppSettings
        {
            Save = new SaveSettings
            {
                ImageFormat = "invalid_format",
                JpgQuality = 150
            },
            Hotkey = new HotkeySettings
            {
                Modifiers = new List<string>(),
                Key = "V"
            }
        };

        var json = JsonSerializer.Serialize(invalidSettings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_testSettingsPath, json);

        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.Current.Save.ImageFormat.Should().Be("png");
        settingsService.Current.Save.JpgQuality.Should().Be(90);
        settingsService.Current.Hotkey.Modifiers.Should().NotBeEmpty();
    }

    [Fact]
    public void SettingsWithNullSave_LoadsDefaultAndBacksUp()
    {
        var invalidSettings = new AppSettings
        {
            Save = null!,
            Hotkey = new HotkeySettings
            {
                Modifiers = new List<string> { "Control" },
                Key = "V"
            },
            Notification = new NotificationSettings(),
            Advanced = new AdvancedSettings()
        };

        var json = JsonSerializer.Serialize(invalidSettings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_testSettingsPath, json);

        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.Current.Save.ImageFormat.Should().Be("png");

        var backupFiles = Directory.GetFiles(_testAppDataPath, "settings.json.backup.*");
        backupFiles.Should().NotBeEmpty();
    }

    [Fact]
    public void MultipleSettingsChanges_AllAppliedInOrder()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        var changeLog = new List<string>();
        settingsService.SettingsChanged += (sender, settings) =>
        {
            changeLog.Add($"Format:{settings.Save.ImageFormat},Quality:{settings.Save.JpgQuality}");
        };

        settingsService.UpdateSettings(s => s.Save.ImageFormat = "jpg");
        settingsService.UpdateSettings(s => s.Save.JpgQuality = 95);

        settingsService.Current.Save.ImageFormat.Should().Be("jpg");
        settingsService.Current.Save.JpgQuality.Should().Be(95);

        changeLog.Should().HaveCount(2);
    }

    [Fact]
    public void InvalidSettingsUpdate_ThrowsException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        var act = () => settingsService.UpdateSettings(s =>
        {
            s.Save.ImageFormat = "invalid_format";
        });

        act.Should().Throw<ArgumentException>();

        settingsService.Current.Save.ImageFormat.Should().Be("png");
    }

    [Fact]
    public void SettingsFileNotExist_CreatesDefaultSettings()
    {
        File.Exists(_testSettingsPath).Should().BeFalse();

        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.Current.Should().NotBeNull();
        settingsService.Current.Save.ImageFormat.Should().Be("png");
        settingsService.Current.Save.JpgQuality.Should().Be(90);

        File.Exists(_testSettingsPath).Should().BeTrue();
    }

    [Fact]
    public void SettingsValidation_WinKeyInModifiers_Invalid()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        var act = () => settingsService.UpdateSettings(s =>
        {
            s.Hotkey.Modifiers = new List<string> { "Win", "V" };
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SettingsValidation_JpgQualityOutOfRange_Invalid()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        var act = () => settingsService.UpdateSettings(s =>
        {
            s.Save.JpgQuality = 0;
        });

        act.Should().Throw<ArgumentException>();

        var act2 = () => settingsService.UpdateSettings(s =>
        {
            s.Save.JpgQuality = 101;
        });

        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SettingsValidation_AllContentTypesDisabled_Invalid()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        var act = () => settingsService.UpdateSettings(s =>
        {
            s.Save.ImageEnabled = false;
            s.Save.TextEnabled = false;
            s.Save.MarkdownEnabled = false;
            s.Save.JsonEnabled = false;
            s.Save.CsvEnabled = false;
        });

        act.Should().Throw<ArgumentException>();

        settingsService.Current.Save.ImageEnabled.Should().BeTrue();
    }

    [Fact]
    public void SettingsNormalization_FileNamePrefixTooLong_IsTrimmed()
    {
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.UpdateSettings(s =>
        {
            s.Save.FileNamePrefix = "12345678901234567";
        });

        settingsService.Current.Save.FileNamePrefix.Should().Be("1234567890123456");
    }

    [Fact]
    public void SettingsNormalization_FileNamePrefixWithInvalidChar_IsSanitized()
    {
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.UpdateSettings(s =>
        {
            s.Save.FileNamePrefix = "CB:TEST";
        });

        settingsService.Current.Save.FileNamePrefix.Should().Be("CB_TEST");
    }

    [Fact]
    public void SettingsValidation_TimestampDisabledAndPrefixEmpty_IsAllowed()
    {
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.UpdateSettings(s =>
        {
            s.Save.IncludeTimestamp = false;
            s.Save.FileNamePrefix = "___";
        });

        settingsService.Current.Save.IncludeTimestamp.Should().BeFalse();
        settingsService.Current.Save.FileNamePrefix.Should().BeEmpty();
    }

    [Fact]
    public void SettingsWithAllContentTypesDisabled_LoadsDefaultAndBacksUp()
    {
        var invalidSettings = new AppSettings
        {
            Save = new SaveSettings
            {
                ImageEnabled = false,
                TextEnabled = false,
                MarkdownEnabled = false,
                JsonEnabled = false,
                CsvEnabled = false,
                ImageFormat = "png",
                JpgQuality = 90
            },
            Hotkey = new HotkeySettings
            {
                Modifiers = new List<string> { "Control" },
                Key = "V"
            },
            Notification = new NotificationSettings(),
            Advanced = new AdvancedSettings()
        };

        var json = JsonSerializer.Serialize(invalidSettings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_testSettingsPath, json);

        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.Current.Save.ImageEnabled.Should().BeTrue();
        settingsService.Current.Save.TextEnabled.Should().BeTrue();
        settingsService.Current.Save.MarkdownEnabled.Should().BeTrue();
        settingsService.Current.Save.JsonEnabled.Should().BeTrue();
        settingsService.Current.Save.CsvEnabled.Should().BeTrue();

        var backupFiles = Directory.GetFiles(_testAppDataPath, "settings.json.backup.*");
        backupFiles.Should().NotBeEmpty();
    }

    [Fact]
    public void ContentTypeSettings_DefaultAllEnabled()
    {
        // Arrange & Act
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.Current.Save.ImageEnabled.Should().BeTrue();
        settingsService.Current.Save.TextEnabled.Should().BeTrue();
        settingsService.Current.Save.MarkdownEnabled.Should().BeTrue();
        settingsService.Current.Save.JsonEnabled.Should().BeTrue();
        settingsService.Current.Save.CsvEnabled.Should().BeTrue();
        settingsService.Current.Save.FileNamePrefix.Should().Be("CS");
        settingsService.Current.Save.IncludeTimestamp.Should().BeTrue();
    }

    [Fact]
    public void ContentTypeSettings_CanBeDisabled()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.UpdateSettings(s =>
        {
            s.Save.MarkdownEnabled = false;
            s.Save.JsonEnabled = false;
        });

        settingsService.Current.Save.ImageEnabled.Should().BeTrue();
        settingsService.Current.Save.TextEnabled.Should().BeTrue();
        settingsService.Current.Save.MarkdownEnabled.Should().BeFalse();
        settingsService.Current.Save.JsonEnabled.Should().BeFalse();
        settingsService.Current.Save.CsvEnabled.Should().BeTrue();

        var savedJson = File.ReadAllText(_testSettingsPath);
        savedJson.Should().Contain("\"markdownEnabled\": false");
        savedJson.Should().Contain("\"jsonEnabled\": false");
    }

    [Fact]
    public void AdvancedSettings_DefaultStartupGuidanceShown_IsFalse()
    {
        // Arrange & Act
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        // Assert
        settingsService.Current.Advanced.StartupGuidanceShown.Should().BeFalse();
    }

    [Fact]
    public void UiLanguage_CanBeUpdatedAndPersisted()
    {
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.UpdateSettings(settings => settings.Ui.Language = AppLanguage.Japanese);

        settingsService.Current.Ui.Language.Should().Be(AppLanguage.Japanese);
        var savedJson = File.ReadAllText(_testSettingsPath);
        savedJson.Should().Contain("\"language\": \"ja\"");
    }

    [Fact]
    public void UiLanguage_InvalidValue_IsNormalizedToEnglish()
    {
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.UpdateSettings(settings => settings.Ui.Language = "fr-FR");

        settingsService.Current.Ui.Language.Should().Be(AppLanguage.English);
    }

    [Fact]
    public void UiLanguage_DefaultIsAlwaysSupported()
    {
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);

        settingsService.Current.Ui.Language.Should().BeOneOf(AppLanguage.English, AppLanguage.Japanese);
    }
}

