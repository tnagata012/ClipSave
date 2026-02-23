using ClipSave.Models;
using FluentAssertions;
using System.Text.Json;

namespace ClipSave.UnitTests;

[UnitTest]
public class AppSettingsTests
{
    [Fact]
    public void HasAnyEnabledContentType_IsNotSerialized()
    {
        var settings = new SaveSettings();

        var json = JsonSerializer.Serialize(settings);

        json.Should().NotContain("hasAnyEnabledContentType");
        json.Should().NotContain("HasAnyEnabledContentType");
    }

    [Fact]
    public void HasAnyEnabledContentType_Default_IsTrue()
    {
        var settings = new SaveSettings();

        settings.HasAnyEnabledContentType.Should().BeTrue();
    }

    [Fact]
    public void HasAnyEnabledContentType_AllDisabled_IsFalse()
    {
        var settings = new SaveSettings
        {
            ImageEnabled = false,
            TextEnabled = false,
            MarkdownEnabled = false,
            JsonEnabled = false,
            CsvEnabled = false
        };

        settings.HasAnyEnabledContentType.Should().BeFalse();
    }

    [Theory]
    [InlineData(ContentType.Image, true)]
    [InlineData(ContentType.Text, true)]
    [InlineData(ContentType.Markdown, true)]
    [InlineData(ContentType.Json, true)]
    [InlineData(ContentType.Csv, true)]
    public void IsContentTypeEnabled_DefaultsToTrue(ContentType type, bool expected)
    {
        var settings = new SaveSettings();

        var result = settings.IsContentTypeEnabled(type);

        result.Should().Be(expected);
    }

    [Fact]
    public void IsContentTypeEnabled_Image_CanBeDisabled()
    {
        var settings = new SaveSettings { ImageEnabled = false };

        settings.IsContentTypeEnabled(ContentType.Image).Should().BeFalse();
        settings.IsContentTypeEnabled(ContentType.Text).Should().BeTrue();
    }

    [Fact]
    public void IsContentTypeEnabled_Text_CanBeDisabled()
    {
        var settings = new SaveSettings { TextEnabled = false };

        settings.IsContentTypeEnabled(ContentType.Text).Should().BeFalse();
        settings.IsContentTypeEnabled(ContentType.Image).Should().BeTrue();
    }

    [Fact]
    public void IsContentTypeEnabled_Markdown_CanBeDisabled()
    {
        var settings = new SaveSettings { MarkdownEnabled = false };

        settings.IsContentTypeEnabled(ContentType.Markdown).Should().BeFalse();
    }

    [Fact]
    public void IsContentTypeEnabled_Json_CanBeDisabled()
    {
        var settings = new SaveSettings { JsonEnabled = false };

        settings.IsContentTypeEnabled(ContentType.Json).Should().BeFalse();
    }

    [Fact]
    public void IsContentTypeEnabled_Csv_CanBeDisabled()
    {
        var settings = new SaveSettings { CsvEnabled = false };

        settings.IsContentTypeEnabled(ContentType.Csv).Should().BeFalse();
    }

    [Fact]
    public void IsContentTypeEnabled_None_ReturnsFalse()
    {
        var settings = new SaveSettings();

        settings.IsContentTypeEnabled(ContentType.None).Should().BeFalse();
    }
}
