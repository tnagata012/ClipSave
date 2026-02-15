using ClipSave.Services;
using FluentAssertions;
using System.IO;

namespace ClipSave.UnitTests;

[UnitTest]
public class ActiveWindowServiceTests : IDisposable
{
    private readonly string _existingDirectory;

    public ActiveWindowServiceTests()
    {
        _existingDirectory = Path.Combine(Path.GetTempPath(), $"ClipSave_ActiveWindow_{Guid.NewGuid()}");
        Directory.CreateDirectory(_existingDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_existingDirectory))
        {
            try
            {
                Directory.Delete(_existingDirectory, true);
            }
            catch
            {
                // cleanup error is ignored
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative\\path")]
    [InlineData("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    public void TryNormalizeExistingDirectoryPath_ReturnsFalse_ForInvalidInput(string? input)
    {
        var result = ActiveWindowService.TryNormalizeExistingDirectoryPath(input, out var normalizedPath);

        result.Should().BeFalse();
        normalizedPath.Should().BeEmpty();
    }

    [Fact]
    public void TryNormalizeExistingDirectoryPath_ReturnsFalse_ForMissingDirectory()
    {
        var missingPath = Path.Combine(_existingDirectory, "missing");

        var result = ActiveWindowService.TryNormalizeExistingDirectoryPath(missingPath, out var normalizedPath);

        result.Should().BeFalse();
        normalizedPath.Should().BeEmpty();
    }

    [Fact]
    public void TryNormalizeExistingDirectoryPath_ReturnsTrue_ForExistingDirectory()
    {
        var pathWithDot = Path.Combine(_existingDirectory, ".");

        var result = ActiveWindowService.TryNormalizeExistingDirectoryPath(pathWithDot, out var normalizedPath);

        result.Should().BeTrue();
        normalizedPath.Should().Be(Path.GetFullPath(pathWithDot));
    }
}
