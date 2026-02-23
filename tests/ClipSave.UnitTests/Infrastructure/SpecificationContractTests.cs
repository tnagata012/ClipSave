using FluentAssertions;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ClipSave.UnitTests;

[UnitTest]
public class SpecificationContractTests
{
    [Fact]
    public void ClipSaveProject_ExposesInternalsToAllTestProjects()
    {
        var projectPath = Path.Combine(TestPaths.SourceRoot, "ClipSave.csproj");
        var document = XDocument.Load(projectPath);
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;

        var internalsVisibleTo = document
            .Descendants(ns + "InternalsVisibleTo")
            .Select(item => item.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        internalsVisibleTo.Should().Contain("ClipSave.UnitTests");
        internalsVisibleTo.Should().Contain("ClipSave.IntegrationTests");
        internalsVisibleTo.Should().Contain("ClipSave.UiTests");
    }

    [Fact]
    public void AppManifest_UsesAsInvokerWithoutUiAccess()
    {
        var manifestPath = Path.Combine(TestPaths.SourceRoot, "app.manifest");
        var document = XDocument.Load(manifestPath);
        XNamespace asmV3 = "urn:schemas-microsoft-com:asm.v3";

        var executionLevel = document.Descendants(asmV3 + "requestedExecutionLevel").SingleOrDefault();
        executionLevel.Should().NotBeNull();
        executionLevel!.Attribute("level")?.Value.Should().Be("asInvoker");
        executionLevel.Attribute("uiAccess")?.Value.Should().Be("false");
    }
}
