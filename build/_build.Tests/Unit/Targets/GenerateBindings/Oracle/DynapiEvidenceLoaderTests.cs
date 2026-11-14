using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class DynapiEvidenceLoaderTests
{
    [Test]
    public async Task LoadAsync_Should_Union_Immediate_Versions_From_Both_Legacy_Roots()
    {
        var exports = FixtureLoader.Load("BindingGeneration/Oracle/SDL2.exports");
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("external/vcpkg/buildtrees/sdl2/src/z/src/dynapi/SDL2.exports", exports.Replace("SDL_Init", "SDL_Quit", StringComparison.Ordinal))
            .WithTextFile("external/vcpkg/buildtrees/sdl2/src/a/src/dynapi/SDL2.exports", exports)
            .WithTextFile("vcpkg_installed/vcpkg/blds/sdl2/src/b/src/dynapi/SDL2.exports", exports.Replace("SDL_Init", "SDL_Free", StringComparison.Ordinal))
            .WithTextFile("external/vcpkg/buildtrees/sdl2/src/deeper/nested/src/dynapi/SDL2.exports", exports.Replace("SDL_Init", "SDL_Ignore", StringComparison.Ordinal));
        var result = await DynapiEvidenceLoader.LoadAsync(world.CakeContext, world.RepoRoot);

        await Assert.That(result.Status).IsEqualTo(SourceStatus.Present);
        await Assert.That(result.Exports).IsEquivalentTo(["SDL_Init", "SDL_Quit", "SDL_Free"], CollectionOrdering.Matching);
    }
}
