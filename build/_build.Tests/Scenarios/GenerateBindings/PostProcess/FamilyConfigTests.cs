using Build.Tests.Fixtures;

namespace Build.Tests.Scenarios.GenerateBindings.PostProcess;

public sealed class FamilyConfigTests
{
    [Test]
    public async Task LoadAsync_Should_Read_Family_Classes_From_The_PathService_Repository_Root()
    {
        var world = PostProcessFixture.World();
        world.Environment.WorkingDirectory = world.RepoRoot.Combine("unrelated");
        var config = await PostProcessFixture.ConfigAsync(world);
        await Assert.That(config.PublicClass("core")).IsEqualTo("SDL");
        await Assert.That(config.RawClass("core")).IsEqualTo("SDLNative");
        await Assert.That(config.PublicClass("gfx")).IsEqualTo("SDL2_gfx");
        await Assert.That(string.Join(',', config.Families())).IsEqualTo("core,image,gfx,ttf,mixer");
    }

    [Test]
    public async Task LoadAsync_Should_Refuse_An_Unsupported_Schema()
    {
        var world = PostProcessFixture.World().WithManifestFile(
            FixtureLoader.Load("Manifest/manifest-real.json").Replace("\"3.0\"", "\"99.0\"", StringComparison.Ordinal));
        var exception = await Assert.ThrowsAsync<Cake.Core.CakeException>(() => PostProcessFixture.ConfigAsync(world));
        await Assert.That(exception!.Message).IsEqualTo("Manifest schema_version '99.0' is unsupported; expected '3.0'.");
    }

    [Test]
    public async Task OpaqueHandles_Should_Include_Pulled_Core_Handles_Without_Changing_Ownership()
    {
        var config = await PostProcessFixture.ConfigAsync(PostProcessFixture.World());
        var (coreAuto, coreForce) = config.OpaqueHandles("core", true);
        await Assert.That(coreAuto.Count).IsEqualTo(14);
        await Assert.That(coreForce.Count).IsEqualTo(3);
        await Assert.That(coreAuto).Contains("SDL_Window");
        await Assert.That(coreForce).Contains("SDL_RWops");
        var (imageAuto, imageForce) = config.OpaqueHandles("image", true);
        await Assert.That(imageAuto).Contains("SDL_Renderer");
        await Assert.That(imageForce).Contains("SDL_RWops");
        var (ttfAuto, ttfForce) = config.OpaqueHandles("ttf", true);
        await Assert.That(ttfAuto).Contains("TTF_Font");
        await Assert.That(ttfAuto).Contains("SDL_Renderer");
        await Assert.That(ttfForce).Contains("SDL_RWops");
        var (ownedAuto, ownedForce) = config.OpaqueHandles("ttf", false);
        await Assert.That(ownedAuto).Contains("TTF_Font");
        await Assert.That(ownedAuto).DoesNotContain("SDL_Renderer");
        await Assert.That(ownedForce).DoesNotContain("SDL_RWops");
        var (mixerAuto, _) = config.OpaqueHandles("mixer", true);
        await Assert.That(mixerAuto).Contains("Mix_Music");
    }
}
