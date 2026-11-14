using Build.Data.BindingGeneration;
using Build.Tests.Fixtures;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class DynapiEvidenceLoaderTests
{
    [Test]
    public async Task LoadAsync_Should_Reject_Different_Sources_From_Both_Roots_Instead_Of_Unioning_Them()
    {
        var exports = FixtureLoader.Load("BindingGeneration/Oracle/SDL2.exports");
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("external/vcpkg/buildtrees/sdl2/src/z/src/dynapi/SDL2.exports", exports.Replace("SDL_Init", "SDL_Quit", StringComparison.Ordinal))
            .WithTextFile("external/vcpkg/buildtrees/sdl2/src/a/src/dynapi/SDL2.exports", exports)
            .WithTextFile("vcpkg_installed/vcpkg/blds/sdl2/src/b/src/dynapi/SDL2.exports", exports.Replace("SDL_Init", "SDL_Free", StringComparison.Ordinal))
            .WithTextFile("external/vcpkg/buildtrees/sdl2/src/deeper/nested/src/dynapi/SDL2.exports", exports.Replace("SDL_Init", "SDL_Ignore", StringComparison.Ordinal));
        var result = await new DynapiManifestRepository(world.CakeContext, world.CreateBuildContext().Paths).LoadAsync();

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Reason).IsEqualTo(
            "SDL2 dynapi manifest glob '/repo/external/vcpkg/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports' matched multiple candidates: " +
            "'/repo/external/vcpkg/buildtrees/sdl2/src/a/src/dynapi/SDL2.exports', '/repo/external/vcpkg/buildtrees/sdl2/src/z/src/dynapi/SDL2.exports', " +
            "'/repo/vcpkg_installed/vcpkg/blds/sdl2/src/b/src/dynapi/SDL2.exports'. " +
            "This usually means a stale buildtree from a previous vcpkg pin was left on disk after a version downgrade. " +
            "Delete the unwanted directory under external/vcpkg/buildtrees/sdl2/src/ before re-running.");
    }
}
