using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ClipSave.IntegrationTests;

[IntegrationTest]
public class FileNamingIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILoggerFactory _loggerFactory;

    public FileNamingIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_FileNaming_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // cleanup best-effort
            }
        }

        _loggerFactory.Dispose();
    }

    [Fact]
    [Spec("SPEC-010-003")]
    [Spec("SPEC-017-005")]
    public async Task TimestampEnabled_AppendsTimestamp()
    {
        var settings = new SaveSettings
        {
            FileNamePrefix = "CS",
            IncludeTimestamp = true
        };

        var filePath = await SaveTextAsync("hello", settings);

        Path.GetFileName(filePath).Should().MatchRegex(@"^CS_\d{8}_\d{6}\.txt$");
    }

    [Fact]
    [Spec("SPEC-017-001")]
    [Spec("SPEC-017-006")]
    public async Task TimestampDisabled_UsesPrefixBase()
    {
        var settings = new SaveSettings
        {
            FileNamePrefix = "CS",
            IncludeTimestamp = false
        };

        var filePath = await SaveTextAsync("hello", settings);

        Path.GetFileName(filePath).Should().Be("CS.txt");
    }

    [Fact]
    [Spec("SPEC-017-007")]
    public async Task FileNaming_TimestampDisabledAndEmptyPrefix_UsesNumericSequenceOnly()
    {
        var settings = new SaveSettings
        {
            FileNamePrefix = "___",
            IncludeTimestamp = false
        };

        var filePath1 = await SaveTextAsync("hello", settings);
        var filePath2 = await SaveTextAsync("hello", settings);

        Path.GetFileName(filePath1).Should().Be("1.txt");
        Path.GetFileName(filePath2).Should().Be("2.txt");
    }

    [Fact]
    [Spec("SPEC-017-002")]
    public async Task DefaultPrefix_UsesCsPrefix()
    {
        var settings = new SaveSettings();

        var filePath = await SaveTextAsync("hello", settings);

        Path.GetFileName(filePath).Should().MatchRegex(@"^CS_\d{8}_\d{6}\.txt$");
    }

    [Fact]
    [Spec("SPEC-017-008")]
    public async Task FileNaming_DuplicatePrefixBasedName_AddsNumericSuffix()
    {
        var settings = new SaveSettings
        {
            FileNamePrefix = "CLIP",
            IncludeTimestamp = false
        };

        var filePath1 = await SaveTextAsync("hello", settings);
        var filePath2 = await SaveTextAsync("hello", settings);

        Path.GetFileName(filePath1).Should().Be("CLIP.txt");
        Path.GetFileName(filePath2).Should().Be("CLIP_1.txt");
    }

    [Fact]
    [Spec("SPEC-017-009")]
    public async Task FileNaming_ReservedDeviceName_IsEscaped()
    {
        var settings = new SaveSettings
        {
            FileNamePrefix = "CON",
            IncludeTimestamp = false
        };

        var filePath = await SaveTextAsync("hello", settings);

        Path.GetFileName(filePath).Should().Be("_CON.txt");
    }

    [Fact]
    [Spec("SPEC-017-003")]
    public async Task FileNaming_PrefixLongerThan16Chars_IsNormalizedTo16Chars()
    {
        var settings = new SaveSettings
        {
            FileNamePrefix = "12345678901234567890",
            IncludeTimestamp = false
        };

        var filePath = await SaveTextAsync("hello", settings);

        Path.GetFileName(filePath).Should().Be("1234567890123456.txt");
    }

    [Fact]
    [Spec("SPEC-017-004")]
    public async Task FileNaming_PrefixWithInvalidChars_IsSanitized()
    {
        var settings = new SaveSettings
        {
            FileNamePrefix = "CB:TEST/ABC",
            IncludeTimestamp = false
        };

        var filePath = await SaveTextAsync("hello", settings);

        Path.GetFileName(filePath).Should().Be("CB_TEST_ABC.txt");
    }

    [Fact]
    public async Task FileNaming_MarkdownHeading_DoesNotChangeName()
    {
        var settings = new SaveSettings
        {
            FileNamePrefix = "CS",
            IncludeTimestamp = false
        };
        var markdown = """
            # Project Plan

            body
            """;

        var filePath = await SaveMarkdownAsync(markdown, settings);

        Path.GetFileName(filePath).Should().Be("CS.md");
    }

    private async Task<string> SaveTextAsync(string text, SaveSettings settings)
    {
        var imageService = new ImageEncodingService(_loggerFactory.CreateLogger<ImageEncodingService>());
        var contentService = new ContentEncodingService(_loggerFactory.CreateLogger<ContentEncodingService>(), imageService);
        var fileService = new FileStorageService(_loggerFactory.CreateLogger<FileStorageService>());

        var content = new TextContent(text);
        var (data, extension) = contentService.Encode(content, settings);

        var options = new FileNamingOptions
        {
            Prefix = settings.FileNamePrefix,
            IncludeTimestamp = settings.IncludeTimestamp
        };

        return await fileService.SaveFileAsync(data, _testDirectory, extension, options);
    }

    private async Task<string> SaveMarkdownAsync(string markdown, SaveSettings settings)
    {
        var imageService = new ImageEncodingService(_loggerFactory.CreateLogger<ImageEncodingService>());
        var contentService = new ContentEncodingService(_loggerFactory.CreateLogger<ContentEncodingService>(), imageService);
        var fileService = new FileStorageService(_loggerFactory.CreateLogger<FileStorageService>());

        var content = new MarkdownContent(markdown);
        var (data, extension) = contentService.Encode(content, settings);

        var options = new FileNamingOptions
        {
            Prefix = settings.FileNamePrefix,
            IncludeTimestamp = settings.IncludeTimestamp
        };

        return await fileService.SaveFileAsync(data, _testDirectory, extension, options);
    }
}

