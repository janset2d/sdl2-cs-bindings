using Build.Targets.GenerateBindings.Translation;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

/// <summary>
/// Family-scoped identity policy. <see cref="CoreOwnedTypeMap"/> answers "does
/// family X own identifier Y?" (prefix match against manifest's
/// <c>owned_prefixes</c>) and builds the cross-family qualified reference for
/// satellite emit contexts. These tests pin the identity-axis surface only.
/// </summary>
public sealed class CoreOwnedTypeMapTests
{
    [Test]
    public void Constructor_Should_Throw_When_Config_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new CoreOwnedTypeMap(null!));
    }

    [Test]
    public async Task IsOwned_Should_Return_True_For_All_Manifest_Owned_Prefixes()
    {
        // sdl2-core declares ["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"] — every
        // prefix must register as owned. Catches a future manifest edit that
        // accidentally drops a prefix.
        var map = new CoreOwnedTypeMap(BindingGenerationFixture.Sdl2CoreConfig());

        await Assert.That(map.IsOwned("SDL_Surface")).IsTrue();
        await Assert.That(map.IsOwned("SDL_Renderer")).IsTrue();
        await Assert.That(map.IsOwned("SDLK_RETURN")).IsTrue();
        await Assert.That(map.IsOwned("SDL_HINT_RENDER_DRIVER")).IsTrue();
        await Assert.That(map.IsOwned("SDL_INIT_VIDEO")).IsTrue();
    }

    [Test]
    public async Task IsOwned_Should_Return_False_For_Satellite_Family_Identifiers()
    {
        // Satellite-owned names (IMG_*, Mix_*, TTF_*) must NOT register as
        // sdl2-core-owned — that's how Stage 2 translator decides whether to
        // emit a local declaration vs a qualified cross-family reference.
        var map = new CoreOwnedTypeMap(BindingGenerationFixture.Sdl2CoreConfig());

        await Assert.That(map.IsOwned("IMG_Load")).IsFalse();
        await Assert.That(map.IsOwned("Mix_OpenAudio")).IsFalse();
        await Assert.That(map.IsOwned("TTF_OpenFont")).IsFalse();
        await Assert.That(map.IsOwned("Net_Init")).IsFalse();
    }

    [Test]
    public async Task IsOwned_Should_Return_False_For_Empty_String()
    {
        var map = new CoreOwnedTypeMap(BindingGenerationFixture.Sdl2CoreConfig());

        await Assert.That(map.IsOwned(string.Empty)).IsFalse();
    }

    [Test]
    public void IsOwned_Should_Throw_When_Identifier_Is_Null()
    {
        var map = new CoreOwnedTypeMap(BindingGenerationFixture.Sdl2CoreConfig());

        Assert.Throws<ArgumentNullException>(() => map.IsOwned(null!));
    }

    [Test]
    public async Task QualifiedManagedReference_Should_Prepend_Janset_Plus_Managed_Namespace()
    {
        // sdl2-core ManagedNamespace = "SDL2" yields the prefix satellite
        // translators emit so satellite source can write Janset.SDL2.SDL_Surface*
        // without redeclaring the core type.
        var map = new CoreOwnedTypeMap(BindingGenerationFixture.Sdl2CoreConfig());

        await Assert.That(map.QualifiedManagedReference("SDL_Surface")).IsEqualTo("Janset.SDL2.SDL_Surface");
        await Assert.That(map.QualifiedManagedReference("SDL_Renderer")).IsEqualTo("Janset.SDL2.SDL_Renderer");
    }

    [Test]
    public async Task CoreFamilyId_Should_Echo_Config_Family_Id()
    {
        var map = new CoreOwnedTypeMap(BindingGenerationFixture.Sdl2CoreConfig());

        await Assert.That(map.CoreFamilyId).IsEqualTo("sdl2-core");
    }
}
