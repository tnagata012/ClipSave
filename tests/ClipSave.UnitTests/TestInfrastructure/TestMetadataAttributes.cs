using Xunit.Abstractions;
using Xunit.Sdk;

namespace ClipSave.UnitTests;

[TraitDiscoverer("ClipSave.UnitTests.UnitTestDiscoverer", "ClipSave.UnitTests")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class UnitTestAttribute : Attribute, ITraitAttribute
{
}

public sealed class UnitTestDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        yield return new KeyValuePair<string, string>("Category", "Unit");
    }
}
