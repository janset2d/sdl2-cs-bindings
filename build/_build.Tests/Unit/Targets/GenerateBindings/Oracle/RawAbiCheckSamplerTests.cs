using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures.BindingGeneration;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class RawAbiCheckSamplerTests
{
    [Test]
    public async Task Sample_Should_Select_Diverse_Categories_Before_Filling_In_Input_Order()
    {
        await Assert.That(RawAbiCheckSampler.Sample(RawAbiCheckSamplerData.MixedCategories, 4).Select(check => check.Symbol))
            .IsEquivalentTo(["A", "E", "F", "B"], CollectionOrdering.Matching);
    }
}
