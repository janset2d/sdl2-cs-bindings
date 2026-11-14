using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class CSharpEvidenceLoaderTests
{
    [Test]
    public async Task Load_Should_Extract_Present_Text_In_Declaration_Order()
    {
        var source = CSharpEvidenceLoader.Load("fixture.cs", FixtureLoader.Load("BindingGeneration/Oracle/mixed-generated.cs.txt"));

        await Assert.That(source.Status).IsEqualTo(SourceStatus.Present);
        await Assert.That(source.Evidence.Functions.Select(function => function.ManagedName))
            .IsEquivalentTo(["SDL_Init", "ManagedHelper", "IMG_Init", "SDL_Quit", "IMG_Quit"], CollectionOrdering.Matching);
    }
}
