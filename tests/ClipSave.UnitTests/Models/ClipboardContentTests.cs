using ClipSave.Models;
using FluentAssertions;
using System.Windows.Media.Imaging;

namespace ClipSave.UnitTests;

public class ClipboardContentTests
{
    #region ContentType

    [Fact]
    public void ImageContent_HasCorrectType()
    {
        // Arrange
        var bitmap = new WriteableBitmap(10, 10, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);

        // Act
        var content = new ImageContent(bitmap);

        // Assert
        content.Type.Should().Be(ContentType.Image);
    }

    [Fact]
    public void TextContent_HasCorrectType()
    {
        // Act
        var content = new TextContent("Hello");

        // Assert
        content.Type.Should().Be(ContentType.Text);
    }

    [Fact]
    public void MarkdownContent_HasCorrectType()
    {
        // Act
        var content = new MarkdownContent("# Title");

        // Assert
        content.Type.Should().Be(ContentType.Markdown);
    }

    [Fact]
    public void JsonContent_HasCorrectType()
    {
        // Act
        var content = new JsonContent("{}", "{}");

        // Assert
        content.Type.Should().Be(ContentType.Json);
    }

    [Fact]
    public void CsvContent_HasCorrectType()
    {
        // Act
        var content = new CsvContent("A\tB\n1\t2", 2, 2);

        // Assert
        content.Type.Should().Be(ContentType.Csv);
    }

    #endregion

    #region GetDescription

    [Fact]
    public void ImageContent_GetDescription_IncludesDimensions()
    {
        // Arrange
        var bitmap = new WriteableBitmap(100, 50, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
        var content = new ImageContent(bitmap);

        // Act
        var description = content.GetDescription();

        // Assert
        description.Should().Contain("100");
        description.Should().Contain("50");
    }

    [Fact]
    public void TextContent_GetDescription_IncludesLength()
    {
        // Arrange
        var content = new TextContent("Hello, World!");

        // Act
        var description = content.GetDescription();

        // Assert
        description.Should().Contain("13");
        description.Should().Contain("chars");
    }

    [Fact]
    public void MarkdownContent_GetDescription_IncludesLength()
    {
        // Arrange
        var content = new MarkdownContent("# Hello World");

        // Act
        var description = content.GetDescription();

        // Assert
        description.Should().Contain("13");
        description.Should().Contain("Markdown");
    }

    [Fact]
    public void JsonContent_GetDescription_IncludesLength()
    {
        // Arrange
        var formatted = """
            {
              "key": "value"
            }
            """;
        var content = new JsonContent("{}", formatted);

        // Act
        var description = content.GetDescription();

        // Assert
        description.Should().Contain("JSON");
    }

    [Fact]
    public void CsvContent_GetDescription_IncludesRowsAndColumns()
    {
        // Arrange
        var content = new CsvContent("A\tB\tC\n1\t2\t3\n4\t5\t6", 3, 3);

        // Act
        var description = content.GetDescription();

        // Assert
        description.Should().Contain("3 rows");
        description.Should().Contain("3 cols");
    }

    #endregion
}
