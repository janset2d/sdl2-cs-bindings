using Build.Data.BindingGeneration.Models;
using Build.Tests.Fixtures;
using Build.Validation.BindingGeneration;
using static Build.Tests.Fixtures.BindingGenerationFixture;

namespace Build.Tests.Unit.Validation.BindingGeneration;

public sealed class DynapiCoherenceValidatorTests
{
    private static HashSet<string> Symbols(params string[] names)
        => new(names, StringComparer.Ordinal);

    [Test]
    public async Task ValidatorId_Should_Be_Stable_Kebab_Case_String()
    {
        var validator = new DynapiCoherenceValidator(new FakeDynapiManifestRepository(null));

        await Assert.That(validator.ValidatorId).IsEqualTo("dynapi-coherence");
    }

    [Test]
    public async Task ValidateAsync_Should_Throw_When_Model_Is_Null()
    {
        var validator = new DynapiCoherenceValidator(new FakeDynapiManifestRepository(null));

        await Assert.That(async () => await validator.ValidateAsync(null!, Sdl2CoreConfig(), CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ValidateAsync_Should_Throw_When_Config_Is_Null()
    {
        var validator = new DynapiCoherenceValidator(new FakeDynapiManifestRepository(null));

        await Assert.That(async () => await validator.ValidateAsync(ModelWithNeutralFunctions("SDL_Init"), null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ValidateAsync_Should_Return_Empty_When_Config_Has_No_Dynapi_Block()
    {
        // Satellites + SDL3 don't have a dynapi manifest; the validator structurally
        // opts out via config.Dynapi == null and doesn't even consult the repository.
        var validator = new DynapiCoherenceValidator(new FakeDynapiManifestRepository(null));
        var config = Sdl2CoreConfig(withDynapi: false);
        var model = ModelWithNeutralFunctions("IMG_Load");

        var report = await validator.ValidateAsync(model, config, CancellationToken.None);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ValidateAsync_Should_Pass_When_Emitted_Matches_Dynapi_Exports_Exactly()
    {
        var validator = new DynapiCoherenceValidator(new FakeDynapiManifestRepository(Symbols("SDL_Init", "SDL_Quit")));
        var model = ModelWithNeutralFunctions("SDL_Init", "SDL_Quit");

        var report = await validator.ValidateAsync(model, Sdl2CoreConfig(), CancellationToken.None);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Errors.Count).IsEqualTo(0);
        await Assert.That(report.Warnings.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Emitted_Symbol_Missing_From_Dynapi()
    {
        // False-positive (SDL_LeakySymbol emitted but not exported) → Error under
        // Stage1Generator severity profile.
        var validator = new DynapiCoherenceValidator(new FakeDynapiManifestRepository(Symbols("SDL_Init")));
        var model = ModelWithNeutralFunctions("SDL_Init", "SDL_LeakySymbol");

        var report = await validator.ValidateAsync(model, Sdl2CoreConfig(), CancellationToken.None);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsGreaterThan(0);
        await Assert.That(report.Errors[0].Message).Contains("SDL_LeakySymbol");
    }

    [Test]
    public async Task ValidateAsync_Should_Warn_When_Dynapi_Export_Missing_From_Emit()
    {
        // False-negative (manifest declares SDL_OrphanExport, emit doesn't have it)
        // → Warning under Stage1Generator severity profile (tuning iteration tolerance).
        var validator = new DynapiCoherenceValidator(new FakeDynapiManifestRepository(Symbols("SDL_Init", "SDL_OrphanExport")));
        var model = ModelWithNeutralFunctions("SDL_Init");

        var report = await validator.ValidateAsync(model, Sdl2CoreConfig(), CancellationToken.None);

        await Assert.That(report.IsValid).IsTrue();   // Warnings don't fail
        await Assert.That(report.Warnings.Count).IsGreaterThan(0);
        await Assert.That(report.Warnings[0].Message).Contains("SDL_OrphanExport");
    }

    [Test]
    public async Task ValidateAsync_Should_Ignore_Configured_Excluded_Functions()
    {
        var validator = new DynapiCoherenceValidator(new FakeDynapiManifestRepository(Symbols("SDL_Init", "SDL_DYNAPI_entry")));
        var model = ModelWithNeutralFunctions("SDL_Init");

        var report = await validator.ValidateAsync(model, Sdl2CoreConfig(), CancellationToken.None);

        await Assert.That(report.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ValidateAsync_Should_Throw_CakeException_When_Dynapi_Manifest_Resolution_Fails()
    {
        var validator = new DynapiCoherenceValidator(
            new FakeDynapiManifestRepository(
                publicSymbols: null,
                error: ManifestResolutionError.NotFound("/fake/glob")));

        await Assert.That(async () => await validator.ValidateAsync(ModelWithNeutralFunctions("SDL_Init"), Sdl2CoreConfig(), CancellationToken.None))
            .Throws<Cake.Core.CakeException>();
    }
}
