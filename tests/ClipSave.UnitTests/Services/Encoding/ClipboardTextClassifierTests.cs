using ClipSave.Models;
using ClipSave.Services;
using FluentAssertions;

namespace ClipSave.UnitTests;

[UnitTest]
public class ClipboardTextClassifierTests
{
    [Fact]
    public void Classify_TabSeparatedMarkdown_PrefersCsv()
    {
        var content = ClipboardTextClassifier.Classify("# Header\tValue\nRow\t1");

        content.Should().BeOfType<CsvContent>();
    }

    [Fact]
    public void Classify_JsonText_ReturnsFormattedJson()
    {
        var content = ClipboardTextClassifier.Classify("""{"name":"test","value":123}""");

        content.Should().BeOfType<JsonContent>();
        ((JsonContent)content).FormattedJson.Should().Contain("\n");
    }

    [Fact]
    public void Classify_Markdown_ReturnsMarkdown()
    {
        var content = ClipboardTextClassifier.Classify("# Title\n\nThis is **bold**.");

        content.Should().BeOfType<MarkdownContent>();
    }

    [Fact]
    public void Classify_InvalidTabularText_FallsBackToText()
    {
        var content = ClipboardTextClassifier.Classify("Name\tAge\nAlice");

        content.Should().BeOfType<TextContent>();
    }
}
