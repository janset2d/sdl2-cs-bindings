using Build.Targets.GenerateBindings.Oracle;

namespace Build.Tests.Fixtures.BindingGeneration;

internal static class RawAbiCheckSamplerData
{
    public static readonly RawAbiCheck[] MixedCategories =
    [
        new("raw-abi-public-class", "error", "Hard Bug", "A", "duplicate", "a.cs"),
        new("raw-abi-public-class", "error", "Hard Bug", "B", "duplicate", "b.cs"),
        new("raw-abi-public-class", "error", "Hard Bug", "C", "duplicate", "c.cs"),
        new("raw-abi-public-class", "error", "Hard Bug", "D", "duplicate", "d.cs"),
        new("raw-abi-public-import", "error", "Hard Bug", "E", "varied", "e.cs"),
        new("platform-sensitive-long", "warning", "Compatibility Risk", "F", "varied", "f.cs")
    ];
}
