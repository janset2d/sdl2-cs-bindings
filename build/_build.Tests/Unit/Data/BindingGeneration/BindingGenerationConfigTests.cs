using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Data.BindingGeneration;

public sealed class BindingGenerationConfigTests
{
    [Test]
    public async Task Equals_Should_Match_Separately_Constructed_Configs_With_Default_ExcludedFunctions()
    {
        var source = BindingGenerationFixture.Sdl2CoreConfig();
        var first = BindingGenerationFixture.CreateWithDefaultExcludedFunctions(source);
        var second = BindingGenerationFixture.CreateWithDefaultExcludedFunctions(source);

        await Assert.That(first).IsEqualTo(second);
    }
}
