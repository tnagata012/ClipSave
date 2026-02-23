using FluentAssertions;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ClipSave.UnitTests;

[UnitTest]
public class LocalizationResourceCompletenessTests
{
    [Fact]
    public void StringsJaResx_HasSameKeys_AsBaseStringsResx()
    {
        var baseResources = LoadResources(GetBaseResxPath());
        var jaResources = LoadResources(GetJapaneseResxPath());

        var missingInJa = baseResources.Keys.Except(jaResources.Keys).OrderBy(key => key).ToArray();
        var extraInJa = jaResources.Keys.Except(baseResources.Keys).OrderBy(key => key).ToArray();

        missingInJa.Should().BeEmpty("Japanese resources must define all base keys.");
        extraInJa.Should().BeEmpty("Japanese resources must not contain unknown keys.");
    }

    [Fact]
    public void StringsJaResx_HasMatchingPlaceholders_AsBaseStringsResx()
    {
        var baseResources = LoadResources(GetBaseResxPath());
        var jaResources = LoadResources(GetJapaneseResxPath());

        var mismatches = new List<string>();

        foreach (var key in baseResources.Keys.OrderBy(k => k))
        {
            if (!jaResources.TryGetValue(key, out var jaValue))
            {
                continue;
            }

            var basePlaceholders = ExtractPlaceholderIndices(baseResources[key]);
            var jaPlaceholders = ExtractPlaceholderIndices(jaValue);
            if (!basePlaceholders.SequenceEqual(jaPlaceholders))
            {
                mismatches.Add(
                    $"{key}: base=[{string.Join(",", basePlaceholders)}], ja=[{string.Join(",", jaPlaceholders)}]");
            }
        }

        mismatches.Should().BeEmpty("Placeholder indices must match between base and Japanese resources.");
    }

    [Fact]
    public void SourceReferencedLocalizationKeys_ExistInBaseStringsResx()
    {
        var baseResources = LoadResources(GetBaseResxPath());
        var sourceFiles = GetSourceFilesToScan().ToArray();
        var referencedKeys = ExtractReferencedKeys(sourceFiles);
        var missingKeys = referencedKeys
            .Where(key => !baseResources.ContainsKey(key))
            .OrderBy(key => key)
            .ToArray();

        missingKeys.Should().BeEmpty("All statically referenced localization keys must exist in Strings.resx.");
    }

    private static Dictionary<string, string> LoadResources(string resxPath)
    {
        var document = XDocument.Load(resxPath);

        return document.Root!
            .Elements("data")
            .Where(data => data.Attribute("name") != null)
            .ToDictionary(
                data => data.Attribute("name")!.Value,
                data => (string?)data.Element("value") ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static int[] ExtractPlaceholderIndices(string value)
    {
        return Regex.Matches(value, @"\{(\d+)(?:,[^}]*)?(?::[^}]*)?\}")
            .Cast<Match>()
            .Select(match => int.Parse(match.Groups[1].Value))
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
    }

    private static HashSet<string> ExtractReferencedKeys(IEnumerable<string> filePaths)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var patterns = new[]
        {
            new Regex(@"Localizer\[(?<key>[A-Za-z0-9_]+)\]", RegexOptions.Compiled),
            new Regex(@"GetString\(""(?<key>[A-Za-z0-9_]+)""\)", RegexOptions.Compiled),
            new Regex(@"Format\(""(?<key>[A-Za-z0-9_]+)""", RegexOptions.Compiled),
            new Regex(@"\bL\(""(?<key>[A-Za-z0-9_]+)""", RegexOptions.Compiled),
            new Regex(@"\bLF\(""(?<key>[A-Za-z0-9_]+)""", RegexOptions.Compiled)
        };

        foreach (var path in filePaths)
        {
            var content = File.ReadAllText(path);
            foreach (var pattern in patterns)
            {
                foreach (Match match in pattern.Matches(content))
                {
                    var key = match.Groups["key"].Value;
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        keys.Add(key);
                    }
                }
            }
        }

        return keys;
    }

    private static IEnumerable<string> GetSourceFilesToScan()
    {
        return Directory.EnumerateFiles(TestPaths.SourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(path =>
                path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetBaseResxPath()
    {
        return Path.Combine(TestPaths.SourceRoot, "Resources", "Strings.resx");
    }

    private static string GetJapaneseResxPath()
    {
        return Path.Combine(TestPaths.SourceRoot, "Resources", "Strings.ja.resx");
    }
}
