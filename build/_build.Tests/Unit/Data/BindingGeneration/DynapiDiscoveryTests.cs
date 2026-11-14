using Build.Data.BindingGeneration;
using Build.Tests.Fixtures;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Data.BindingGeneration;

public sealed class DynapiDiscoveryTests
{
    [Test]
    public async Task LoadAsync_Should_Read_Installed_Buildtrees_And_Collapse_Identical_Sources()
    {
        var content = FixtureLoader.Load("BindingGeneration/Oracle/SDL2.exports");
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("vcpkg_installed/vcpkg/blds/sdl2/src/version/src/dynapi/SDL2.exports", content);
        var repository = new DynapiManifestRepository(world.CakeContext, world.CreateBuildContext().Paths);
        var installed = await repository.LoadAsync();
        await Assert.That(installed.IsSuccess).IsTrue();
        await Assert.That(installed.Value.PublicSymbols).IsEquivalentTo(["SDL_Init"], CollectionOrdering.Matching);
        await Assert.That(installed.Value.SourcePath).IsEqualTo("/repo/vcpkg_installed/vcpkg/blds/sdl2/src/version/src/dynapi/SDL2.exports");

        world.WithTextFile("external/vcpkg/buildtrees/sdl2/src/version/src/dynapi/SDL2.exports", content);
        var mirrored = await repository.LoadAsync();
        await Assert.That(mirrored.IsSuccess).IsTrue();
        await Assert.That(mirrored.Value.PublicSymbols).IsEquivalentTo(["SDL_Init"], CollectionOrdering.Matching);
        await Assert.That(mirrored.Value.SourcePath).IsEqualTo("/repo/external/vcpkg/buildtrees/sdl2/src/version/src/dynapi/SDL2.exports");

        world.WithTextFile("external/vcpkg/buildtrees/sdl2/src/z/src/dynapi/SDL2.exports", content);
        var duplicate = await repository.LoadAsync();
        await Assert.That(duplicate.IsSuccess).IsTrue();
        await Assert.That(duplicate.Value.PublicSymbols).IsEquivalentTo(["SDL_Init"], CollectionOrdering.Matching);
        await Assert.That(duplicate.Value.SourcePath).IsEqualTo("/repo/external/vcpkg/buildtrees/sdl2/src/version/src/dynapi/SDL2.exports");
    }
}
