using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Translation;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class MacroConstantMergerTests
{
    [Test]
    public async Task Merge_Should_Coalesce_Compatible_Same_Name_Definitions()
    {
        var first = Constant("SDL_HINT_RENDER_DRIVER", "ReadOnlySpan<byte>", "\"SDL_RENDER_DRIVER\"u8");
        var second = Constant("SDL_HINT_RENDER_DRIVER", "ReadOnlySpan<byte>", "\"SDL_RENDER_DRIVER\"u8");

        var result = MacroConstantMerger.Merge([
            new MacroValueClassification(first, Report("SDL_HINT_RENDER_DRIVER", "Neutral")),
            new MacroValueClassification(second, Report("SDL_HINT_RENDER_DRIVER", "Linux")),
        ]);

        await Assert.That(result.Constants.Count).IsEqualTo(1);
        await Assert.That(result.DuplicateCoalescedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Merge_Should_Throw_When_Same_Name_Definitions_Are_Incompatible()
    {
        var first = Constant("SDL_HINT_RENDER_DRIVER", "ReadOnlySpan<byte>", "\"SDL_RENDER_DRIVER\"u8");
        var second = Constant("SDL_HINT_RENDER_DRIVER", "ReadOnlySpan<byte>", "\"BROKEN\"u8");

        await Assert.That(() => MacroConstantMerger.Merge([
            new MacroValueClassification(first, Report("SDL_HINT_RENDER_DRIVER", "Neutral")),
            new MacroValueClassification(second, Report("SDL_HINT_RENDER_DRIVER", "Linux")),
        ])).Throws<InvalidOperationException>()
            .WithMessageContaining("Incompatible macro constant definitions for 'SDL_HINT_RENDER_DRIVER'", StringComparison.Ordinal);
    }

    private static BindingConstant Constant(string name, string type, string value) =>
        new(name, BindingGenerationFixture.NativePrimitive(type, type), value, ConstantKind.Literal);

    private static MacroConstantReportEntry Report(string name, string view) =>
        new(
            Name: name,
            SourceHeader: "SDL_hints.h",
            ParseViewName: view,
            Disposition: "emitted",
            Reason: "safe macro value",
            EmittedType: "ReadOnlySpan<byte>",
            EmittedValue: "\"SDL_RENDER_DRIVER\"u8",
            MacroForm: "object-like",
            Taxonomy: "public-literal-constant",
            OriginalExpression: "\"SDL_RENDER_DRIVER\"",
            ComputedValue: null);
}
