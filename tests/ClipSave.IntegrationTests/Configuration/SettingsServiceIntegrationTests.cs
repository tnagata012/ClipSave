using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows.Media.Imaging;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
[Collection("SettingsTests")]
public class SettingsServiceIntegrationTests : IDisposable
{
    private readonly string _testAppDataPath;
    private readonly string _settingsFilePath;
    private readonly string _originalAppData;
    private readonly ILoggerFactory _loggerFactory;

    public SettingsServiceIntegrationTests()
    {
        _testAppDataPath = Path.Combine(Path.GetTempPath(), $"ClipSave_Settings_Integration_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testAppDataPath);
        _settingsFilePath = Path.Combine(_testAppDataPath, "settings.json");

        _originalAppData = Environment.GetEnvironmentVariable("APPDATA") ?? "";

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
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
            catch { }
        }

        _loggerFactory.Dispose();
    }

    [Fact]
    [Spec("SPEC-040-001")]
    [Spec("SPEC-020-001")]
    public void SettingsChange_ImmediateAndPersisted()
    {
        // Arrange: update settings in the first service instance.
        var service1 = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _testAppDataPath);

        service1.UpdateSettings(s =>
        {
            s.Save.ImageFormat = "jpg";
            s.Save.JpgQuality = 75;
            s.Hotkey.Modifiers = new List<string> { "Control", "Alt" };
            s.Hotkey.Key = "S";
            s.Notification.OnSuccess = true;
            s.Notification.OnNoContent = false;
            s.Notification.OnError = true;
        });

        // Assert: changes are reflected immediately.
        service1.Current.Save.ImageFormat.Should().Be("jpg");
        service1.Current.Save.JpgQuality.Should().Be(75);
        service1.Current.Hotkey.Modifiers.Should().Equal(new[] { "Control", "Alt" });
        service1.Current.Hotkey.Key.Should().Be("S");
        service1.Current.Notification.OnSuccess.Should().BeTrue();
        service1.Current.Notification.OnNoContent.Should().BeFalse();
        service1.Current.Notification.OnError.Should().BeTrue();

        // Assert: settings are persisted to file.
        File.Exists(_settingsFilePath).Should().BeTrue();
        File.ReadAllText(_settingsFilePath).Should().NotContain("\"enabled\"");
        File.ReadAllText(_settingsFilePath).Should().Contain("\"onNoContent\": false");
        File.ReadAllText(_settingsFilePath).Should().NotContain("\"onNoImage\"");

        // Act: create a new service instance to reload from file.
        var service2 = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _testAppDataPath);

        // Assert: persisted settings are restored.
        service2.Current.Save.ImageFormat.Should().Be("jpg");
        service2.Current.Save.JpgQuality.Should().Be(75);
        service2.Current.Hotkey.Modifiers.Should().Equal(new[] { "Control", "Alt" });
        service2.Current.Hotkey.Key.Should().Be("S");
        service2.Current.Notification.OnSuccess.Should().BeTrue();
        service2.Current.Notification.OnNoContent.Should().BeFalse();
        service2.Current.Notification.OnError.Should().BeTrue();
    }

    [Fact]
    [Spec("SPEC-040-002")]
    [Spec("SPEC-040-005")]
    public void CorruptedSettingsFile_FallsBackToDefaults()
    {
        // Arrange: write a corrupted settings file.
        File.WriteAllText(_settingsFilePath, "{ invalid json ///");

        // Act
        var service = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _testAppDataPath);

        // Assert: service falls back to defaults.
        service.Current.Should().NotBeNull();
        service.Current.Save.ImageFormat.Should().Be("png");
        service.Current.Save.JpgQuality.Should().Be(90);

        // Corruption event should be queued.
        service.TryDequeuePendingCorruption(out var corruptionArgs).Should().BeTrue();
        corruptionArgs.Should().NotBeNull();
        corruptionArgs!.BackupPath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Spec("SPEC-040-001")]
    public void SettingsEventPropagation()
    {
        // Arrange
        var service = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _testAppDataPath);
        var eventFired = false;
        Models.AppSettings? receivedSettings = null;

        service.SettingsChanged += (sender, settings) =>
        {
            eventFired = true;
            receivedSettings = settings;
        };

        // Act
        service.UpdateSettings(s => s.Save.ImageFormat = "jpg");

        // Assert
        eventFired.Should().BeTrue();
        receivedSettings.Should().NotBeNull();
        receivedSettings!.Save.ImageFormat.Should().Be("jpg");
    }

    [Fact]
    [Spec("SPEC-040-002")]
    public void MissingSettingsFile_CreatesDefault()
    {
        // Arrange: ensure settings file does not exist.
        File.Exists(_settingsFilePath).Should().BeFalse();

        // Act
        var service = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _testAppDataPath);

        // Trigger save by changing any setting.
        service.UpdateSettings(s => s.Save.ImageFormat = "png");

        // Assert
        File.Exists(_settingsFilePath).Should().BeTrue();
    }

    [Fact]
    [Spec("SPEC-040-001")]
    public async Task SettingsChange_AffectsSaveFormat()
    {
        // Arrange
        var settingsService = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _testAppDataPath);
        var imageService = new ImageEncodingService(_loggerFactory.CreateLogger<ImageEncodingService>());
        var fileService = new FileStorageService(_loggerFactory.CreateLogger<FileStorageService>());

        // Start with PNG format.
        settingsService.UpdateSettings(s =>
        {
            s.Save.ImageFormat = "png";
        });

        var bitmap = CreateTestBitmap(30, 30, hasAlpha: false);

        // Save as PNG.
        var pngData = imageService.EncodeToPng(bitmap);
        var result1 = await fileService.SaveFileAsync(pngData, _testAppDataPath, "png");
        result1.Should().EndWith(".png");

        // Act: switch to JPG and save again.
        settingsService.UpdateSettings(s =>
        {
            s.Save.ImageFormat = "jpg";
        });

        var settings = settingsService.Current.Save;
        var extension = settings.ImageFormat == "jpg" ? "jpg" : "png";
        var data = extension == "jpg"
            ? imageService.EncodeToJpeg(bitmap, settings.JpgQuality)
            : imageService.EncodeToPng(bitmap);

        var result2 = await fileService.SaveFileAsync(data, _testAppDataPath, extension);

        // Assert: format change is applied immediately.
        result2.Should().EndWith(".jpg");
        File.Exists(result2).Should().BeTrue();
    }

    [Fact]
    [Spec("SPEC-040-006")]
    public void AllContentTypesDisabled_UpdateIsRejected()
    {
        var service = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _testAppDataPath);

        var act = () => service.UpdateSettings(settings =>
        {
            settings.Save.ImageEnabled = false;
            settings.Save.TextEnabled = false;
            settings.Save.MarkdownEnabled = false;
            settings.Save.JsonEnabled = false;
            settings.Save.CsvEnabled = false;
        });

        act.Should().Throw<ArgumentException>();
        service.Current.Save.HasAnyEnabledContentType.Should().BeTrue();
    }

    [Fact]
    [Spec("SPEC-020-004")]
    public void HotkeyWithoutModifiers_UpdateIsRejected()
    {
        var service = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _testAppDataPath);

        var act = () => service.UpdateSettings(settings =>
        {
            settings.Hotkey.Modifiers = new List<string>();
            settings.Hotkey.Key = "V";
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Spec("SPEC-020-005")]
    public void HotkeyWithWinModifier_UpdateIsRejected()
    {
        var service = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _testAppDataPath);

        var act = () => service.UpdateSettings(settings =>
        {
            settings.Hotkey.Modifiers = new List<string> { "Win" };
            settings.Hotkey.Key = "V";
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Spec("SPEC-090-003")]
    [Spec("SPEC-090-008")]
    public void InvalidUiLanguage_UpdateFallsBackToEnglish()
    {
        var service = new SettingsService(_loggerFactory.CreateLogger<SettingsService>(), _testAppDataPath);

        service.UpdateSettings(settings => settings.Ui.Language = "fr-FR");

        service.Current.Ui.Language.Should().Be("en");
    }

    private static WriteableBitmap CreateTestBitmap(int width, int height, bool hasAlpha)
    {
        var format = hasAlpha
            ? System.Windows.Media.PixelFormats.Bgra32
            : System.Windows.Media.PixelFormats.Bgr32;

        var bitmap = new WriteableBitmap(width, height, 96, 96, format, null);
        var bytesPerPixel = format.BitsPerPixel / 8;
        var pixels = new byte[width * height * bytesPerPixel];
        var random = new Random(42);

        for (int i = 0; i < pixels.Length; i += bytesPerPixel)
        {
            pixels[i] = (byte)random.Next(256);     // B
            pixels[i + 1] = (byte)random.Next(256); // G
            pixels[i + 2] = (byte)random.Next(256); // R
            if (hasAlpha && bytesPerPixel == 4)
            {
                pixels[i + 3] = (byte)random.Next(128, 256); // A
            }
        }

        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * bytesPerPixel, 0);
        bitmap.Freeze();

        return bitmap;
    }
}

