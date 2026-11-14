using Build.Targets.GenerateBindings.Oracle;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class RawAbiCheckSamplerTests
{
    [Test]
    public async Task Sample_Should_Select_Diverse_Categories_Before_Filling_In_Input_Order()
    {
        RawAbiCheck[] checks =
        [
            new("raw-abi-public-class", "error", "Hard Bug", "A", "duplicate", "a.cs"),
            new("raw-abi-public-class", "error", "Hard Bug", "B", "duplicate", "b.cs"),
            new("raw-abi-public-class", "error", "Hard Bug", "C", "duplicate", "c.cs"),
            new("raw-abi-public-class", "error", "Hard Bug", "D", "duplicate", "d.cs"),
            new("raw-abi-public-import", "error", "Hard Bug", "E", "varied", "e.cs"),
            new("platform-sensitive-long", "warning", "Compatibility Risk", "F", "varied", "f.cs")
        ];

        await Assert.That(RawAbiCheckSampler.Sample(checks, 4).Select(check => check.Symbol))
            .IsEquivalentTo(["A", "E", "F", "B"], CollectionOrdering.Matching);
    }
}
