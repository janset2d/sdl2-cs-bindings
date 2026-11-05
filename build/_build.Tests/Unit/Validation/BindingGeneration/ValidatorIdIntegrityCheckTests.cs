using System.Collections.Immutable;
using Build.Tests.Fixtures;
using Build.Validation.BindingGeneration;
using Cake.Core;

namespace Build.Tests.Unit.Validation.BindingGeneration;

public sealed class ValidatorIdIntegrityCheckTests
{
    [Test]
    public async Task AssertKnown_Should_Reject_Enabled_Unknown_Ids_When_Multiple_Are_Configured()
    {
        var comparer = EqualityComparer<string>.Create(StringComparer.Ordinal.Equals, key => key switch
        {
            "z-unknown" => 1,
            "known" => 2,
            "a-unknown" => 3,
            "disabled" => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        });
        var config = BindingGenerationFixture.Sdl2CoreConfig() with
        {
            Validators = ImmutableDictionary.Create<string, bool>(comparer).Add("z-unknown", true).Add("known", true)
                .Add("a-unknown", true).Add("disabled", false),
        };
        var exception = Assert.Throws<CakeException>(() => ValidatorIdIntegrityCheck.AssertKnown(config, ["known"]));
        await Assert.That(exception.Message).IsEqualTo(
            "Binding generation family 'sdl2-core' enables unknown validator id(s): a-unknown, z-unknown. " +
            "Register the validator or fix build/manifest.json library_manifests[].binding_generation.validators.");
    }

    [Test]
    public async Task AssertKnown_Should_Accept_Known_Ids_When_Unknown_Id_Is_Disabled()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig() with
        {
            Validators = ImmutableDictionary<string, bool>.Empty.Add("known", true).Add("unknown", false),
        };
        await Assert.That(() => ValidatorIdIntegrityCheck.AssertKnown(config, ["known"])).ThrowsNothing();
    }
}
