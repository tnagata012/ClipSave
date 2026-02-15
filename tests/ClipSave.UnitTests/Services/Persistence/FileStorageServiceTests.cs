using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;

namespace ClipSave.UnitTests;

[UnitTest]
public class FileStorageServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileStorageService _fileStorageService;

    public FileStorageServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_FileStorageTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var logger = Mock.Of<ILogger<FileStorageService>>();
        _fileStorageService = new FileStorageService(logger);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_testDirectory))
        {
            return;
        }

        Directory.Delete(_testDirectory, true);
    }

    [Fact]
    public async Task SaveFileAsync_WritesProvidedBytes()
    {
        var data = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

        var filePath = await _fileStorageService.SaveFileAsync(data, _testDirectory, "bin");
        var written = await File.ReadAllBytesAsync(filePath);

        File.Exists(filePath).Should().BeTrue();
        written.Should().Equal(data);
    }

    [Fact]
    public async Task SaveFileAsync_NonExistentDirectory_CreatesAutomatically()
    {
        var nonExistentDir = Path.Combine(_testDirectory, "SubFolder", "DeepFolder");
        var testData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var filePath = await _fileStorageService.SaveFileAsync(testData, nonExistentDir, "png");

        Directory.Exists(nonExistentDir).Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveFileAsync_DuplicateFileName_AddsSequentialSuffix()
    {
        var testData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var filePath1 = await _fileStorageService.SaveFileAsync(testData, _testDirectory, "png");
        var filePath2 = await _fileStorageService.SaveFileAsync(testData, _testDirectory, "png");
        var filePath3 = await _fileStorageService.SaveFileAsync(testData, _testDirectory, "png");

        File.Exists(filePath1).Should().BeTrue();
        File.Exists(filePath2).Should().BeTrue();
        File.Exists(filePath3).Should().BeTrue();
        filePath1.Should().NotBe(filePath2);
        filePath2.Should().NotBe(filePath3);
        filePath1.Should().NotBe(filePath3);
        Path.GetFileName(filePath1).Should().MatchRegex(@"^CS_\d{8}_\d{6}\.png$");
    }

    [Fact]
    public async Task SaveFileAsync_ExtensionWithDot_IsNormalized()
    {
        var testData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var filePath = await _fileStorageService.SaveFileAsync(testData, _testDirectory, ".PNG");

        File.Exists(filePath).Should().BeTrue();
        filePath.Should().EndWith(".png");
    }

    [Fact]
    public async Task SaveFileAsync_NonFileSystemDirectory_ThrowsArgumentException()
    {
        var testData = new byte[] { 0x01, 0x02, 0x03 };

        var act = async () => await _fileStorageService.SaveFileAsync(
            testData,
            "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
            "png");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveFileAsync_InvalidExtension_ThrowsArgumentException()
    {
        var testData = new byte[] { 0x01, 0x02, 0x03 };

        var act = async () => await _fileStorageService.SaveFileAsync(testData, _testDirectory, "p/ng");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveFileAsync_TimestampDisabledAndPrefixEmpty_UsesNumericSequenceOnly()
    {
        var testData = new byte[] { 0x01, 0x02, 0x03 };
        var options = new FileNamingOptions
        {
            Prefix = "___",
            IncludeTimestamp = false
        };

        var filePath1 = await _fileStorageService.SaveFileAsync(testData, _testDirectory, "txt", options);
        var filePath2 = await _fileStorageService.SaveFileAsync(testData, _testDirectory, "txt", options);

        Path.GetFileName(filePath1).Should().Be("1.txt");
        Path.GetFileName(filePath2).Should().Be("2.txt");
    }

    [Fact]
    public async Task SaveFileAsync_CustomPrefixAndNoTimestamp_UsesPrefixName()
    {
        var testData = new byte[] { 0x01, 0x02, 0x03 };
        var options = new FileNamingOptions
        {
            Prefix = "CLIP",
            IncludeTimestamp = false
        };

        var filePath = await _fileStorageService.SaveFileAsync(testData, _testDirectory, "png", options);

        Path.GetFileName(filePath).Should().Be("CLIP.png");
    }

    [Fact]
    public async Task SaveFileAsync_ReservedDeviceName_IsEscaped()
    {
        var testData = new byte[] { 0x01, 0x02, 0x03 };
        var options = new FileNamingOptions
        {
            Prefix = "CON",
            IncludeTimestamp = false
        };

        var filePath = await _fileStorageService.SaveFileAsync(testData, _testDirectory, "txt", options);

        Path.GetFileName(filePath).Should().Be("_CON.txt");
    }

    [Fact]
    public async Task SaveFileAsync_ReservedDeviceNameWithSuffix_IsEscaped()
    {
        var testData = new byte[] { 0x01, 0x02, 0x03 };
        var options = new FileNamingOptions
        {
            Prefix = "CON.backup",
            IncludeTimestamp = false
        };

        var filePath = await _fileStorageService.SaveFileAsync(testData, _testDirectory, "txt", options);

        Path.GetFileName(filePath).Should().Be("_CON.backup.txt");
    }

    [Fact]
    public void HasEnoughSpace_ReturnsTrue_ForSmallPayload()
    {
        var result = _fileStorageService.HasEnoughSpace(_testDirectory, 1024);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasEnoughSpace_ReturnsFalse_ForHugePayloadRequirement()
    {
        var result = _fileStorageService.HasEnoughSpace(_testDirectory, long.MaxValue);

        result.Should().BeFalse();
    }
}
