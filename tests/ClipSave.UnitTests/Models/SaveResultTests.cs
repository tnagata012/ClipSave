using ClipSave.Models;
using FluentAssertions;

namespace ClipSave.UnitTests;

[UnitTest]
public class SaveResultTests
{
    [Fact]
    public void CreateSuccess_SetsCorrectProperties()
    {
        // Act
        var result = SaveResult.CreateSuccess("/path/to/file.png", ContentType.Image);

        // Assert
        result.Success.Should().BeTrue();
        result.Kind.Should().Be(SaveResultKind.Success);
        result.FilePath.Should().Be("/path/to/file.png");
        result.ContentType.Should().Be(ContentType.Image);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void CreateSuccess_WithTextContent()
    {
        // Act
        var result = SaveResult.CreateSuccess("/path/to/file.txt", ContentType.Text);

        // Assert
        result.ContentType.Should().Be(ContentType.Text);
    }

    [Fact]
    public void CreateSuccess_WithMarkdownContent()
    {
        // Act
        var result = SaveResult.CreateSuccess("/path/to/file.md", ContentType.Markdown);

        // Assert
        result.ContentType.Should().Be(ContentType.Markdown);
    }

    [Fact]
    public void CreateSuccess_WithJsonContent()
    {
        // Act
        var result = SaveResult.CreateSuccess("/path/to/file.json", ContentType.Json);

        // Assert
        result.ContentType.Should().Be(ContentType.Json);
    }

    [Fact]
    public void CreateSuccess_WithCsvContent()
    {
        // Act
        var result = SaveResult.CreateSuccess("/path/to/file.csv", ContentType.Csv);

        // Assert
        result.ContentType.Should().Be(ContentType.Csv);
    }

    [Fact]
    public void CreateFailure_SetsCorrectProperties()
    {
        // Act
        var result = SaveResult.CreateFailure("An error occurred.");

        // Assert
        result.Success.Should().BeFalse();
        result.Kind.Should().Be(SaveResultKind.Error);
        result.ErrorMessage.Should().Be("An error occurred.");
        result.FilePath.Should().BeNull();
    }

    [Fact]
    public void CreateNoContent_SetsCorrectProperties()
    {
        // Act
        var result = SaveResult.CreateNoContent();

        // Assert
        result.Success.Should().BeFalse();
        result.Kind.Should().Be(SaveResultKind.NoContent);
        result.ErrorMessage.Should().Contain("clipboard");
    }

    [Fact]
    public void CreateUnsupportedWindow_SetsCorrectProperties()
    {
        // Act
        var result = SaveResult.CreateUnsupportedWindow();

        // Assert
        result.Success.Should().BeFalse();
        result.Kind.Should().Be(SaveResultKind.UnsupportedWindow);
    }

    [Fact]
    public void CreateBusy_SetsCorrectProperties()
    {
        // Act
        var result = SaveResult.CreateBusy();

        // Assert
        result.Success.Should().BeFalse();
        result.Kind.Should().Be(SaveResultKind.Busy);
        result.ErrorMessage.Should().Be("A save operation is already in progress.");
    }

    [Theory]
    [InlineData(ContentType.Image, "image")]
    [InlineData(ContentType.Text, "text")]
    [InlineData(ContentType.Markdown, "Markdown")]
    [InlineData(ContentType.Json, "JSON")]
    [InlineData(ContentType.Csv, "CSV")]
    public void CreateContentTypeDisabled_IncludesContentTypeName(ContentType type, string expectedName)
    {
        // Act
        var result = SaveResult.CreateContentTypeDisabled(type);

        // Assert
        result.Success.Should().BeFalse();
        result.Kind.Should().Be(SaveResultKind.ContentTypeDisabled);
        result.ContentType.Should().Be(type);
        result.ErrorMessage.Should().Contain(expectedName);
    }
}
