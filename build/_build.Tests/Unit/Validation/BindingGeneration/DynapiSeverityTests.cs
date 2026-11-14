using Build.Results;
using Build.Tests.Fixtures;
using Build.Validation.BindingGeneration;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Validation.BindingGeneration;

public sealed class DynapiSeverityTests
{
    [Test]
    public async Task ValidateAsync_Should_Use_The_Explicit_Profile_Without_Changing_The_Default()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("external/vcpkg/buildtrees/sdl2/src/v/src/dynapi/SDL2.exports", FixtureLoader.Load("BindingGeneration/Oracle/SDL2.exports"));
        var validator = new DynapiCoherenceValidator(new Build.Data.BindingGeneration.DynapiManifestRepository(world.CakeContext, world.CreateBuildContext().Paths));
        var emitted = new HashSet<string>(["SDL_Extra"], StringComparer.Ordinal);
        var config = ManifestFixture.RealCoreLibrary.BindingGeneration;
        var stage1 = await validator.ValidateAsync(emitted, config, CancellationToken.None);
        var strict = await validator.ValidateAsync(emitted, config, SeverityProfile.Stage2Strict, CancellationToken.None);

        await Assert.That(stage1.Select(check => (check.Name, check.Severity)))
            .IsEquivalentTo([("Emitted binding references unexported symbol", ValidationSeverity.Error),
                ("Public SDL2 export missing from emitted bindings", ValidationSeverity.Warning)], CollectionOrdering.Matching);
        await Assert.That(strict.Select(check => (check.Name, check.Severity)))
            .IsEquivalentTo([("Emitted binding references unexported symbol", ValidationSeverity.Error),
                ("Public SDL2 export missing from emitted bindings", ValidationSeverity.Error)], CollectionOrdering.Matching);
        await Assert.That(strict.Select(check => check.Message))
            .IsEquivalentTo(stage1.Select(check => check.Message), CollectionOrdering.Matching);
        await Assert.That(strict.First().Message).Contains("'SDL_Extra'");
        await Assert.That(strict.Last().Message).Contains("'SDL_Init'");
    }
}
