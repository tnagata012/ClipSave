using Xunit.Abstractions;
using Xunit.Sdk;

namespace ClipSave.UiTests;

[TraitDiscoverer("ClipSave.UiTests.UiTestDiscoverer", "ClipSave.UiTests")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class UiTestAttribute : Attribute, ITraitAttribute
{
}

public sealed class UiTestDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        yield return new KeyValuePair<string, string>("Category", "UI");
    }
}

[TraitDiscoverer("ClipSave.UiTests.SpecDiscoverer", "ClipSave.UiTests")]
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
