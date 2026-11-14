using System.Collections.Immutable;
using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Data.Manifest;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Data.BindingGeneration;

/// <summary>
/// Sociable round-trip coverage for <see cref="BindingGenerationConfigRepository"/>.
/// Flows through the real <see cref="ManifestRepository"/> + fake filesystem so the
/// typed deserialization path is
/// exercised end-to-end. Pairs with <see cref="BindingGenerationConfigRepositoryUnitTests"/>.
/// </summary>
public sealed class BindingGenerationConfigRepositoryRoundTripTests
{
    [Test]
    [Arguments("sdl2-core", "SDL2", "SDL", "SDLNative", "SDL2", "Janset.SDL2.Core", true, "2.32.10")]
    [Arguments("sdl2-image", "SDL2.Image", "SDL_image", "SDL_imageNative", "SDL2_image", "Janset.SDL2.Image", false, "2.8.8")]
    [Arguments("sdl2-gfx", "SDL2.Gfx", "SDL2_gfx", "SDL2_gfxNative", "SDL2_gfx", "Janset.SDL2.Gfx", false, "1.0.4")]
    [Arguments("sdl2-ttf", "SDL2.Ttf", "SDL_ttf", "SDL_ttfNative", "SDL2_ttf", "Janset.SDL2.Ttf", true, "2.24.0")]
    [Arguments("sdl2-mixer", "SDL2.Mixer", "SDL_mixer", "SDL_mixerNative", "SDL2_mixer", "Janset.SDL2.Mixer", true, "2.8.1")]
    public async Task Load_Should_Resolve_And_Derive_The_Family_Identity(
        string family, string ns, string primary, string raw, string library, string project, bool owner, string version)
    {
        var profile = BuildRepo("Manifest/manifest-real.json").Load(family).Value;
        await Assert.That(profile.FamilyId).IsEqualTo(family);
        await Assert.That(profile.ManagedNamespace).IsEqualTo(ns);
        await Assert.That(profile.PrimaryClassName).IsEqualTo(primary);
        await Assert.That(profile.RawClassName).IsEqualTo(raw);
        await Assert.That(profile.LibraryName).IsEqualTo(library);
        await Assert.That(profile.LibraryVersion).IsEqualTo(version);
        await Assert.That(profile.GeneratedRoot).IsEqualTo($"spikes/binding-generators/clangsharp/src/{project}/Generated");
        await Assert.That(profile.GenerationProject).IsEqualTo($"spikes/binding-generators/clangsharp/src/{project}/{project}.csproj");
        await Assert.That(profile.OwnerMode).IsEqualTo(owner);
    }

    [Test]
    public async Task LoadGlobalSettings_Should_Preserve_Explicit_Order_And_Undefine_Only_Macros()
    {
        var settings = BuildRepo("Manifest/manifest-real.json").LoadGlobalSettings();
        await Assert.That(settings.FamilyOrder).IsEquivalentTo(
            ["sdl2-core", "sdl2-image", "sdl2-gfx", "sdl2-ttf", "sdl2-mixer"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(settings.PlatformViews.Select(view => view.Name)).IsEquivalentTo(
            ["WindowsDesktop", "WinRT", "GDK", "Linux", "MacOS", "IOS", "Android"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(settings.AllPlatformMacros).Contains("SDL_VIDEO_DRIVER_DIRECTFB");
        await Assert.That(settings.AllPlatformMacros).Contains("SDL_VIDEO_DRIVER_OS2");
        await Assert.That(settings.BaseRsp).IsEqualTo("build/binding-generation/rsp/base.rsp");
        await Assert.That(settings.PerHeaderRspDir).IsEqualTo("build/binding-generation/rsp/per-header");
        await Assert.That(settings.PlatformHeaderShimsDir).IsEqualTo("build/binding-generation/shims/platform-headers");
    }

    /// <summary>
    /// Builds a real <see cref="ManifestRepository"/> against a fake filesystem seeded
    /// with the given fixture JSON, then wraps it in the
    /// <see cref="BindingGenerationConfigRepository"/> under test. Sociable by design —
    /// any manifest-deserialization regression surfaces here as a <c>CakeException</c>
    /// from the underlying repository.
    /// </summary>
    private static BindingGenerationConfigRepository BuildRepo(string fixtureRelativePath)
    {
        var world = FakeCakeWorld.CreateWindows();
        world.WithManifestFile(FixtureLoader.Load(fixtureRelativePath));
        var manifestPath = world.RepoRoot.CombineWithFilePath("build/manifest.json");
        var manifestRepo = new ManifestRepository(world.CakeContext, manifestPath);
        return new BindingGenerationConfigRepository(manifestRepo);
    }

    [Test]
    public async Task Load_Should_Return_Sdl2Core_Config_When_Manifest_Has_Enabled_Block()
    {
        var repo = BuildRepo("Manifest/manifest-real.json");

        var result = repo.Load("sdl2-core");

        await Assert.That(result.IsSuccess).IsTrue();
        var config = result.Value;
        await Assert.That(config.FamilyId).IsEqualTo("sdl2-core");
        await Assert.That(config.Enabled).IsTrue();
        await Assert.That(config.ManagedNamespace).IsEqualTo("SDL2");
        await Assert.That(config.PrimaryClassName).IsEqualTo("SDL");
        await Assert.That(config.OwnedPrefixes).Contains("SDL_");
        await Assert.That(config.OwnedPrefixes).Contains("SDLK_");
        await Assert.That(config.OwnedPrefixes).Contains("SDL_HINT_");
        await Assert.That(config.OwnedPrefixes).Contains("SDL_INIT_");
        await Assert.That(config.ExcludedFunctions).Contains("SDL_main");
        await Assert.That(config.ExcludedFunctions).Contains("SDL_DYNAPI_entry");
        await Assert.That(config.HeaderSet).IsNotNull();
        await Assert.That(config.HeaderSet!.IncludeDir).IsEqualTo("include/SDL2");
        await Assert.That(config.HeaderSet.Headers.Count).IsEqualTo(51);
        await Assert.That(config.RequiredFunctions.Count).IsEqualTo(2);
        await Assert.That(config.RequiredFunctions[0].Name).IsEqualTo("SDL_Init");
        await Assert.That(config.RequiredConstants.Count).IsEqualTo(2);
        await Assert.That(config.RequiredConstants[0].Name).IsEqualTo("SDL_INIT_TIMER");
        await Assert.That(config.RequiredConstants[0].Kind).IsEqualTo(ConstantKind.Literal);
        await Assert.That(config.RequiredConstants[0].Value).IsEqualTo("0x00000001u");
        await Assert.That(config.RequiredConstants[1].Name).IsEqualTo("SDL_INIT_EVERYTHING");
        await Assert.That(config.RequiredConstants[1].Kind).IsEqualTo(ConstantKind.Computed);
        await Assert.That(config.RequiredConstants[0].AllowStale).IsFalse();
        await Assert.That(config.RequiredConstants[0].Reason).IsNull();
        await Assert.That(config.Validators["dynapi-coherence"]).IsTrue();
        await Assert.That(config.Dynapi).IsNotNull();
    }

    [Test]
    public async Task Load_Should_Preserve_Opaque_And_Flags_Audit_Metadata()
    {
        var config = BuildRepo("Manifest/manifest-real.json").Load("sdl2-core").Value;
        var handle = config.OpaqueHandles.AutoDetectWellKnown.Single(entry => entry.Name == "SDL_Window");
        await Assert.That(handle.Header).IsEqualTo("SDL_video.h");
        await Assert.That(handle.HeaderDecl).IsEqualTo("typedef struct SDL_Window SDL_Window;");
        await Assert.That(handle.WikiUrl).IsEqualTo("https://wiki.libsdl.org/SDL2/SDL_Window");
        await Assert.That(handle.WikiEvidence).IsEqualTo("The opaque type used to identify a window.");
        await Assert.That(config.OpaqueHandles.LastAudited).IsEqualTo("2026-05-26");
        await Assert.That(config.FlagsEnums.AllowList.Single(entry => entry.Name == "SDL_Keymod").Reason)
            .IsEqualTo("Composite-alias bitmask; KMOD_CTRL combines KMOD_LCTRL and KMOD_RCTRL.");
    }

    [Test]
    public async Task Load_Should_Stamp_FamilyId_From_Repository_Lookup_Not_From_Json()
    {
        // FamilyId is [JsonIgnore]; the repo `with`-expression stamps it from the
        // lookup key so consumers can read config.FamilyId without re-mapping
        // from LibraryManifest.Name themselves.
        var repo = BuildRepo("Manifest/manifest-real.json");

        var result = repo.Load("sdl2-core");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.FamilyId).IsEqualTo("sdl2-core");
    }

    [Test]
    public async Task Load_Should_Return_FamilyNotFound_When_Family_Missing()
    {
        var repo = BuildRepo("Manifest/manifest-real.json");

        var result = repo.Load("sdl9-nonexistent");

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error.Kind).IsEqualTo(BindingGenerationConfigErrorKind.FamilyNotFound);
    }

    [Test]
    public async Task Load_Should_Return_Disabled_When_Family_Has_Enabled_False()
    {
        var world = FakeCakeWorld.CreateWindows();
        var manifest = ManifestFixture.RealManifest;
        world.WithManifestObject(manifest with
        {
            LibraryManifests = manifest.LibraryManifests
                .Select(library => library.Name == "SDL2_image"
                    ? library with { BindingGeneration = library.BindingGeneration with { Enabled = false } }
                    : library)
                .ToImmutableList(),
        });
        var repo = new BindingGenerationConfigRepository(new ManifestRepository(world.CakeContext, world.RepoRoot.CombineWithFilePath("build/manifest.json")));

        var result = repo.Load("sdl2-image");

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error.Kind).IsEqualTo(BindingGenerationConfigErrorKind.Disabled);
        await Assert.That(repo.EnumerateEnabledFamilies()).IsEquivalentTo(
            ["sdl2-core", "sdl2-gfx", "sdl2-ttf", "sdl2-mixer"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task EnumerateEnabledFamilies_Should_Return_Only_Enabled_Family_Ids()
    {
        var repo = BuildRepo("Manifest/manifest-real.json");

        var enabled = repo.EnumerateEnabledFamilies();

        await Assert.That(string.Join(',', enabled)).IsEqualTo("sdl2-core,sdl2-image,sdl2-gfx,sdl2-ttf,sdl2-mixer");
    }

    [Test]
    public async Task EnumerateEnabledFamilies_Should_Return_Empty_When_Manifest_Has_No_Libraries()
    {
        // manifest-minimal.json has library_manifests: [].
        var repo = BuildRepo("Manifest/manifest-minimal.json");

        var enabled = repo.EnumerateEnabledFamilies();

        await Assert.That(enabled.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Load_Should_Bubble_CakeException_When_Manifest_Missing()
    {
        // Pre-deserialization failures (missing file, malformed JSON, missing required
        // field) surface as CakeException from ManifestRepository — those aren't part
        // of BindingGenerationConfigError's discriminator space. This test pins that
        // contract so a future refactor that swallows them gets caught.
        var world = FakeCakeWorld.CreateWindows();
        // CreateWindows seeds manifest-win-x64.json; nuke it to simulate file-missing.
        var missingPath = world.RepoRoot.CombineWithFilePath("build/does-not-exist.json");
        var manifestRepo = new ManifestRepository(world.CakeContext, missingPath);
        var repo = new BindingGenerationConfigRepository(manifestRepo);

        await Assert.That(() => repo.Load("sdl2-core"))
            .Throws<Cake.Core.CakeException>();
    }
}
