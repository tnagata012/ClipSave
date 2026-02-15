using ClipSave.Services;
using FluentAssertions;

namespace ClipSave.UnitTests;

public class DelimitedTextCodecTests
{
    [Fact]
    public void TryParseTabSeparated_QuotedMultilineField_IsSingleCell()
    {
        var text = "Name\tNote\nAlice\t\"Line1\nLine2\"";

        var success = DelimitedTextCodec.TryParseTabSeparated(text, out var rows);

        success.Should().BeTrue();
        rows.Should().HaveCount(2);
        rows[0].Should().Equal("Name", "Note");
        rows[1].Should().Equal("Alice", "Line1\nLine2");
    }

    [Fact]
    public void TryParseTabSeparated_QuotedTab_IsSingleCell()
    {
        var text = "Name\tValue\nAlice\t\"A\tB\"";

        var success = DelimitedTextCodec.TryParseTabSeparated(text, out var rows);

        success.Should().BeTrue();
        rows.Should().HaveCount(2);
        rows[1].Should().Equal("Alice", "A\tB");
    }

    [Fact]
    public void TryParseTabSeparated_UnclosedQuote_ReturnsFalse()
    {
        var text = "A\t\"B";

        var success = DelimitedTextCodec.TryParseTabSeparated(text, out var rows);

        success.Should().BeFalse();
        rows.Should().BeEmpty();
    }

    [Fact]
    public void ConvertTabSeparatedToCsv_QuotedMultilineField_IsEscaped()
    {
        var text = "Name\tNote\nAlice\t\"Line1\nLine2\"";

        var csv = DelimitedTextCodec.ConvertTabSeparatedToCsv(text);

        csv.Should().Be("Name,Note\r\nAlice,\"Line1\nLine2\"\r\n");
    }
}
