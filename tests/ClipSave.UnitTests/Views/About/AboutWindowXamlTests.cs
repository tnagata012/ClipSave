using FluentAssertions;
using System.IO;
using System.Text.RegularExpressions;

namespace ClipSave.UnitTests;

public class AboutWindowXamlTests
{
    [Fact]
    public void LocalizerBindings_AreOneWay()
    {
        var xamlPath = Path.Combine(TestPaths.SourceRoot, "Views", "About", "AboutWindow.xaml");
        var xaml = File.ReadAllText(xamlPath);

        var bindings = Regex.Matches(xaml, @"\{Binding\s+Localizer\[[^\]]+\][^}]*\}")
            .Select(match => match.Value)
            .ToList();

        bindings.Should().NotBeEmpty();
        bindings.Should().OnlyContain(binding => binding.Contains("Mode=OneWay", StringComparison.Ordinal));
    }
}
