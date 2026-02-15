using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.Windows.Media.Imaging;

namespace ClipSave.UnitTests;

public class ContentEncodingServiceTests
{
    private readonly ContentEncodingService _service;
    private readonly SaveSettings _defaultSettings;

    public ContentEncodingServiceTests()
    {
        var contentLogger = Mock.Of<ILogger<ContentEncodingService>>();
        var imageLogger = Mock.Of<ILogger<ImageEncodingService>>();
        var imageService = new ImageEncodingService(imageLogger);
        _service = new ContentEncodingService(contentLogger, imageService);
        _defaultSettings = new SaveSettings();
    }

    [Fact]
    public void Encode_TextContent_ReturnsUtf8Bytes()
    {
        // Arrange
        var content = new TextContent("Hello, World!");

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        // Assert
        extension.Should().Be("txt");
        var text = Encoding.UTF8.GetString(data);
        text.Should().Be("Hello, World!");
    }

    [Fact]
    public void Encode_TextContent_HandlesJapanese()
    {
        // Arrange
        var content = new TextContent("こんにちは世界");

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        // Assert
        extension.Should().Be("txt");
        var text = Encoding.UTF8.GetString(data);
        text.Should().Be("こんにちは世界");
    }

    [Fact]
    public void Encode_TextContent_HandlesMultilineText()
    {
        // Arrange
        var content = new TextContent("Line 1\nLine 2\r\nLine 3");

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        // Assert
        var text = Encoding.UTF8.GetString(data);
        text.Should().Contain("Line 1");
        text.Should().Contain("Line 2");
        text.Should().Contain("Line 3");
    }

    [Fact]
    public void Encode_MarkdownContent_ReturnsUtf8Bytes()
    {
        // Arrange
        var content = new MarkdownContent("# Hello World\n\nThis is **bold** text.");

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        // Assert
        extension.Should().Be("md");
        var text = Encoding.UTF8.GetString(data);
        text.Should().Be("# Hello World\n\nThis is **bold** text.");
    }

    [Fact]
    public void Encode_MarkdownContent_HandlesComplexMarkdown()
    {
        // Arrange
        var markdown = """
            # Title
            
            - Item 1
            - Item 2
            
            ```csharp
            var x = 1;
            ```
            """;
        var content = new MarkdownContent(markdown);

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        // Assert
        extension.Should().Be("md");
        var text = Encoding.UTF8.GetString(data);
        text.Should().Contain("# Title");
        text.Should().Contain("- Item 1");
        text.Should().Contain("```csharp");
    }

    [Fact]
    public void Encode_JsonContent_ReturnsFormattedJson()
    {
        // Arrange
        var raw = """{"name":"test","value":123}""";
        var formatted = """
            {
              "name": "test",
              "value": 123
            }
            """;
        var content = new JsonContent(raw, formatted);

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        // Assert
        extension.Should().Be("json");
        var text = Encoding.UTF8.GetString(data);
        text.Should().Be(formatted);
    }

    [Fact]
    public void Encode_JsonContent_PreservesFormatting()
    {
        var formatted = """
            {
              "array": [1, 2, 3],
              "nested": {
                "key": "value"
              }
            }
            """;
        var content = new JsonContent("{}", formatted);

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        // Assert
        var text = Encoding.UTF8.GetString(data);
        text.Should().Contain("\"array\":");
        text.Should().Contain("\"nested\":");
    }

    [Fact]
    public void Encode_CsvContent_ConvertsTabs()
    {
        // Arrange
        var tabText = "A\tB\tC\n1\t2\t3";
        var content = new CsvContent(tabText, 2, 3);

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        // Assert
        extension.Should().Be("csv");

        var textWithoutBom = data.Skip(3).ToArray();
        var text = Encoding.UTF8.GetString(textWithoutBom);
        text.Should().Contain("A,B,C");
        text.Should().Contain("1,2,3");
    }

    [Fact]
    public void Encode_CsvContent_HasBom()
    {
        // Arrange
        var tabText = "A\tB\n1\t2";
        var content = new CsvContent(tabText, 2, 2);

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        data.Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF });
    }

    [Fact]
    public void Encode_CsvContent_EscapesCommas()
    {
        var tabText = "Name\tValue\nHello, World\t123";
        var content = new CsvContent(tabText, 2, 2);

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        // Assert
        var textWithoutBom = Encoding.UTF8.GetString(data.Skip(3).ToArray());
        textWithoutBom.Should().Contain("\"Hello, World\"");
    }

    [Fact]
    public void Encode_CsvContent_EscapesQuotes()
    {
        var tabText = "Name\tValue\nSay \"Hello\"\t123";
        var content = new CsvContent(tabText, 2, 2);

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        // Assert
        var textWithoutBom = Encoding.UTF8.GetString(data.Skip(3).ToArray());
        textWithoutBom.Should().Contain("\"Say \"\"Hello\"\"\"");
    }

    [Fact]
    public void Encode_CsvContent_PreservesQuotedMultilineField()
    {
        var tabText = "Name\tNote\nAlice\t\"Line1\nLine2\"";
        var content = new CsvContent(tabText, 2, 2);

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        // Assert
        extension.Should().Be("csv");
        var textWithoutBom = Encoding.UTF8.GetString(data.Skip(3).ToArray());
        textWithoutBom.Should().Be("Name,Note\r\nAlice,\"Line1\nLine2\"\r\n");
    }

    [Fact]
    public void Encode_CsvContent_PreservesQuotedTabInField()
    {
        var tabText = "Name\tValue\nAlice\t\"A\tB\"";
        var content = new CsvContent(tabText, 2, 2);

        // Act
        var (data, extension) = _service.Encode(content, _defaultSettings);

        // Assert
        extension.Should().Be("csv");
        var textWithoutBom = Encoding.UTF8.GetString(data.Skip(3).ToArray());
        textWithoutBom.Should().Be("Name,Value\r\nAlice,A\tB\r\n");
    }

    [Fact]
    public void Encode_ImageContent_AsPng()
    {
        // Arrange
        var bitmap = new WriteableBitmap(
            10, 10, 96, 96,
            System.Windows.Media.PixelFormats.Bgr32, null);
        var content = new ImageContent(bitmap);
        var settings = new SaveSettings { ImageFormat = "png" };

        // Act
        var (data, extension) = _service.Encode(content, settings);

        // Assert
        extension.Should().Be("png");
        data.Take(4).Should().Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header
    }

    [Fact]
    public void Encode_ImageContent_AsJpg()
    {
        // Arrange
        var bitmap = new WriteableBitmap(
            10, 10, 96, 96,
            System.Windows.Media.PixelFormats.Bgr32, null);
        var content = new ImageContent(bitmap);
        var settings = new SaveSettings { ImageFormat = "jpg" };

        // Act
        var (data, extension) = _service.Encode(content, settings);

        // Assert
        extension.Should().Be("jpg");
        data.Take(3).Should().Equal(new byte[] { 0xFF, 0xD8, 0xFF }); // JPEG header
    }
}
