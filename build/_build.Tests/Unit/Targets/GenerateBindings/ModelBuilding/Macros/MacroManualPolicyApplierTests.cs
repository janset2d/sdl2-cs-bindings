using System.Collections.Immutable;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding.Macros;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Macros;

public sealed class MacroManualPolicyApplierTests
{
    [Test]
    public async Task Apply_Should_Add_Required_Constant_When_Source_Does_Not_Provide_It()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig(requiredConstants:
        [
            BindingGenerationFixture.RequiredConstant("SDL_INIT_TIMER"),
        ]);

        var result = MacroManualPolicyApplier.Apply([], [], config);

        await Assert.That(result.Constants.Select(c => c.Name).ToArray()).IsEquivalentTo(["SDL_INIT_TIMER"]);
    }

    [Test]
    public async Task Apply_Should_Throw_When_Required_Constant_Is_Redundant_And_Not_Allowed_Stale()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig(requiredConstants:
        [
            BindingGenerationFixture.RequiredConstant("SDL_HINT_RENDER_DRIVER", type: "ReadOnlySpan<byte>", value: "\"SDL_RENDER_DRIVER\"u8", sourceHeader: "SDL_hints.h"),
        ]);
        var generated = Constant("SDL_HINT_RENDER_DRIVER", "ReadOnlySpan<byte>", "\"SDL_RENDER_DRIVER\"u8");

        await Assert.That(() => MacroManualPolicyApplier.Apply([generated], [], config))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("Manual required constant 'SDL_HINT_RENDER_DRIVER' is redundant", StringComparison.Ordinal);
    }

    [Test]
    public async Task Apply_Should_Remove_Excluded_Generated_Constant()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig() with
        {
            MacroConstants = new MacroConstantPolicyConfig
            {
                Excluded = ImmutableDictionary<string, ManualMacroConstantPolicyEntry>.Empty.Add(
                    "SDL_PRIVATE_HEADER_SWITCH",
                    new ManualMacroConstantPolicyEntry { Reason = "Not public API." }),
            },
        };

        var result = MacroManualPolicyApplier.Apply([Constant("SDL_PRIVATE_HEADER_SWITCH", "int", "1")], [], config);

        await Assert.That(result.Constants).IsEmpty();
        await Assert.That(result.ExcludedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Apply_Should_Throw_When_Exclude_Is_Unused_And_Not_Allowed_Stale()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig() with
        {
            MacroConstants = new MacroConstantPolicyConfig
            {
                Excluded = ImmutableDictionary<string, ManualMacroConstantPolicyEntry>.Empty.Add(
                    "SDL_MISSING",
                    new ManualMacroConstantPolicyEntry { Reason = "Should be present." }),
            },
        };

        await Assert.That(() => MacroManualPolicyApplier.Apply([], [], config))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("Manual macro exclusion 'SDL_MISSING' was not consumed", StringComparison.Ordinal);
    }

    [Test]
    public async Task Apply_Should_Replace_Generated_Constant_With_Override()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig() with
        {
            MacroConstants = new MacroConstantPolicyConfig
            {
                Overrides = ImmutableDictionary<string, MacroConstantOverrideConfig>.Empty.Add(
                    "SDL_FIXTURE_OVERRIDE",
                    new MacroConstantOverrideConfig
                    {
                        Type = "uint",
                        Value = "42u",
                        SourceHeader = "SDL_fixture.h",
                        Kind = ConstantKind.Literal,
                        Reason = "Fixture override.",
                    }),
            },
        };

        var result = MacroManualPolicyApplier.Apply([Constant("SDL_FIXTURE_OVERRIDE", "uint", "1u")], [], config);

        await Assert.That(result.Constants.Single().Value).IsEqualTo("42u");
        await Assert.That(result.OverriddenCount).IsEqualTo(1);
    }

    private static BindingConstant Constant(string name, string type, string value) =>
        new(name, BindingGenerationFixture.NativePrimitive(type, type), value, ConstantKind.Literal);
}
