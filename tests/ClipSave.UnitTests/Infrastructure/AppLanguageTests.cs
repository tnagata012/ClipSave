using ClipSave.Infrastructure;
using FluentAssertions;
using System.Globalization;

namespace ClipSave.UnitTests;

[UnitTest]
public class AppLanguageTests
{
    [Fact]
    public void Normalize_NullWithoutSystemFallback_ReturnsEnglish()
    {
        var language = AppLanguage.Normalize(null, useSystemWhenMissing: false);

        language.Should().Be(AppLanguage.English);
    }

    [Theory]
    [InlineData("ja", AppLanguage.Japanese)]
    [InlineData("ja-JP", AppLanguage.Japanese)]
    [InlineData("en", AppLanguage.English)]
    [InlineData("en-US", AppLanguage.English)]
    [InlineData("fr-FR", AppLanguage.English)]
    public void Normalize_MapsToSupportedLanguage(string input, string expected)
    {
        var language = AppLanguage.Normalize(input, useSystemWhenMissing: false);

        language.Should().Be(expected);
    }

    [Theory]
    [InlineData("japanese", AppLanguage.English)]
    [InlineData("jaJP", AppLanguage.English)]
    [InlineData("ja_", AppLanguage.English)]
    [InlineData("  ja-JP  ", AppLanguage.Japanese)]
    public void Normalize_UsesStrictLanguageCodeMatching(string input, string expected)
    {
        var language = AppLanguage.Normalize(input, useSystemWhenMissing: false);

        language.Should().Be(expected);
    }

    [Fact]
    public void ResolveFromSystem_JapaneseCulture_ReturnsJapanese()
    {
        var language = AppLanguage.ResolveFromSystem(CultureInfo.GetCultureInfo("ja-JP"));

        language.Should().Be(AppLanguage.Japanese);
    }

    [Fact]
    public void ResolveFromSystem_EnglishCulture_ReturnsEnglish()
    {
        var language = AppLanguage.ResolveFromSystem(CultureInfo.GetCultureInfo("en-US"));

        language.Should().Be(AppLanguage.English);
    }
}

