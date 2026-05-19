using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Data.Manifest;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Data.BindingGeneration;

/// <summary>
/// Mock-based unit coverage for <see cref="BindingGenerationConfigRepository"/> —
/// constructor argument validation. Load + EnumerateEnabledFamilies behaviour is
/// covered by <see cref="BindingGenerationConfigRepositoryRoundTripTests"/> using
/// <see cref="ManifestRepository"/> against the fake filesystem.
/// </summary>
public sealed class BindingGenerationConfigRepositoryUnitTests
{
    [Test]
    public async Task Constructor_Should_Throw_When_ManifestRepository_Is_Null()
    {
        await Assert.That(() => new BindingGenerationConfigRepository(null!))
            .Throws<ArgumentNullException>();
    }
}

/// <summary>
/// Sociable round-trip coverage for <see cref="BindingGenerationConfigRepository"/>.
/// Flows through the real <see cref="ManifestRepository"/> + fake filesystem so the
/// typed deserialization path (manifest v2.2 required binding_generation field) is
/// exercised end-to-end. Pairs with the unit suite above per the repository-cohort
/// convention used across <c>Build.Tests.Unit.Data</c>.
/// </summary>
public sealed class BindingGenerationConfigRepositoryRoundTripTests
{
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
        await Assert.That(config.PlatformCatalogId).IsEqualTo("sdl2-core");
        await Assert.That(config.OwnedPrefixes).Contains("SDL_");
        await Assert.That(config.OwnedPrefixes).Contains("SDLK_");
        await Assert.That(config.OwnedPrefixes).Contains("SDL_HINT_");
        await Assert.That(config.OwnedPrefixes).Contains("SDL_INIT_");
        await Assert.That(config.ParseDefines).Contains("SDL_DECLSPEC=");
        await Assert.That(config.ClangArgs).Contains("-fdeclspec");
        await Assert.That(config.ClangArgs).Contains("-U__has_builtin");
        await Assert.That(config.ExcludedFunctions).Contains("SDL_main");
        await Assert.That(config.ExcludedFunctions).Contains("SDL_DYNAPI_entry");
        await Assert.That(config.HeaderSet).IsNotNull();
        await Assert.That(config.HeaderSet!.IncludeDirGlob).IsEqualTo("include/SDL2");
        await Assert.That(config.HeaderSet.HeaderGlob).IsEqualTo("*.h");
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
        await Assert.That(config.MacroConstants.Excluded).IsEmpty();
        await Assert.That(config.MacroConstants.Overrides).IsEmpty();
        await Assert.That(config.DeferredDeclarations.ContainsKey("SDL_SysWMinfo")).IsTrue();
        await Assert.That(config.DeferredDeclarations["SDL_SysWMinfo"].Category).IsEqualTo("deferred-to-stage-2");
        await Assert.That(config.DeferredDeclarations.ContainsKey("SDL_SysWMmsg")).IsTrue();
        await Assert.That(config.DeferredDeclarations["SDL_SysWMmsg"].Category).IsEqualTo("deferred-to-stage-2");
        await Assert.That(config.DeferredDeclarations.ContainsKey("SDL_DUMMY_ENUM")).IsTrue();
        await Assert.That(config.DeferredDeclarations["SDL_DUMMY_ENUM"].Category).IsEqualTo("internal-sdl-sentinel");
        await Assert.That(config.Validators["dynapi-coherence"]).IsTrue();
        await Assert.That(config.Validators["semantic-type-consistency"]).IsTrue();
        await Assert.That(config.Dynapi).IsNotNull();
        await Assert.That(config.Dynapi!.ExportsGlob).IsEqualTo("buildtrees/sdl2/src/*/src/dynapi/SDL2.exports");
    }

    [Test]
    public async Task Load_Should_Return_MacroPolicy_Config_When_Manifest_Has_MacroPolicy_Block()
    {
        var repo = BuildRepo("Manifest/manifest-with-macro-policy.json");

        var result = repo.Load("sdl2-core");

        await Assert.That(result.IsSuccess).IsTrue();
        var config = result.Value;
        await Assert.That(config.MacroConstants.Excluded.ContainsKey("SDL_PRIVATE_HEADER_SWITCH")).IsTrue();
        await Assert.That(config.MacroConstants.Excluded["SDL_PRIVATE_HEADER_SWITCH"].Reason)
            .IsEqualTo("Fixture-only non-API macro exclusion.");
        await Assert.That(config.MacroConstants.Excluded["SDL_PRIVATE_HEADER_SWITCH"].AllowStale).IsTrue();
        await Assert.That(config.MacroConstants.Overrides.ContainsKey("SDL_FIXTURE_OVERRIDE")).IsTrue();
        var macroOverride = config.MacroConstants.Overrides["SDL_FIXTURE_OVERRIDE"];
        await Assert.That(macroOverride.Type).IsEqualTo("uint");
        await Assert.That(macroOverride.Value).IsEqualTo("42u");
        await Assert.That(macroOverride.SourceHeader).IsEqualTo("SDL_fixture.h");
        await Assert.That(macroOverride.Kind).IsEqualTo(ConstantKind.Literal);
        await Assert.That(macroOverride.Reason).IsEqualTo("Fixture-only override.");
        await Assert.That(macroOverride.AllowStale).IsFalse();
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
        // sdl2-image entry in manifest-real.json is a stage-2-placeholder
        // (binding_generation.enabled = false).
        var repo = BuildRepo("Manifest/manifest-real.json");

        var result = repo.Load("sdl2-image");

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error.Kind).IsEqualTo(BindingGenerationConfigErrorKind.Disabled);
    }

    [Test]
    public async Task EnumerateEnabledFamilies_Should_Return_Only_Enabled_Family_Ids()
    {
        // manifest-real.json: sdl2-core enabled=true; sdl2-image/mixer/ttf/gfx enabled=false.
        var repo = BuildRepo("Manifest/manifest-real.json");

        var enabled = repo.EnumerateEnabledFamilies();

        await Assert.That(enabled.Count).IsEqualTo(1);
        await Assert.That(enabled[0]).IsEqualTo("sdl2-core");
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
