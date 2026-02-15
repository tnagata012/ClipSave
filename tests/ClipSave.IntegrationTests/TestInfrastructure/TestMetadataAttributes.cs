using Xunit.Abstractions;
using Xunit.Sdk;

namespace ClipSave.IntegrationTests;

[TraitDiscoverer("ClipSave.IntegrationTests.IntegrationTestDiscoverer", "ClipSave.IntegrationTests")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class IntegrationTestAttribute : Attribute, ITraitAttribute
{
}

public sealed class IntegrationTestDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        yield return new KeyValuePair<string, string>("Category", "Integration");
    }
}

[TraitDiscoverer("ClipSave.IntegrationTests.SpecDiscoverer", "ClipSave.IntegrationTests")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class SpecAttribute : Attribute, ITraitAttribute
{
    public SpecAttribute(string specId)
    {
        SpecId = specId;
    }

    public string SpecId { get; }
}

public sealed class SpecDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var specId = traitAttribute.GetConstructorArguments()
            .OfType<string>()
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(specId))
        {
            yield return new KeyValuePair<string, string>("SpecId", specId);
        }
    }
}
