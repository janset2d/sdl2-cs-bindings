using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Translation;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class MacroValueClassifierTests
{
    [Test]
    public async Task Classify_Should_Emit_String_Literal_As_ReadOnlySpan_Utf8()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\""));

        await Assert.That(result.Constant).IsNotNull();
        await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("ReadOnlySpan<byte>");
        await Assert.That(result.Constant.Value).IsEqualTo("\"SDL_RENDER_DRIVER\"u8");
        await Assert.That(result.Report.Disposition).IsEqualTo("emitted");
    }

    [Test]
    public async Task Classify_Should_Emit_Unsigned_Hex_Literal_As_Uint()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDL_INIT_TIMER", "0x00000001u"));

        await Assert.That(result.Constant).IsNotNull();
        await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("uint");
        await Assert.That(result.Constant.Value).IsEqualTo("0x00000001u");
    }

    [Test]
    public async Task Classify_Should_Emit_Decimal_Literal_As_Int()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDL_BUTTON_LEFT", "1"));

        await Assert.That(result.Constant).IsNotNull();
        await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("int");
        await Assert.That(result.Constant.Value).IsEqualTo("1");
    }

    [Test]
    public async Task Classify_Should_Emit_Escape_Character_Literal_As_Int()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDLK_ESCAPE_FIXTURE", "'\\033'"));

        await Assert.That(result.Constant).IsNotNull();
        await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("int");
        await Assert.That(result.Constant.Value).IsEqualTo("27");
    }

    [Test]
    public async Task Classify_Should_Report_Unsupported_Expression()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDL_UNSAFE_EXPR", "(SDL_SOMETHING(type, value))"));

        await Assert.That(result.Constant).IsNull();
        await Assert.That(result.Report.Disposition).IsEqualTo("unsupported");
        await Assert.That(result.Report.Reason).IsEqualTo("unsupported macro expression");
    }

    [Test]
    public async Task Classify_Should_Emit_Unsigned_Shift_Expression_As_Uint()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDL_HAPTIC_RAMP", "(1u << 6)"));

        await Assert.That(result.Constant).IsNotNull();
        await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("uint");
        await Assert.That(result.Constant.Value).IsEqualTo("64u");
        await Assert.That(result.Constant.Kind).IsEqualTo(ConstantKind.Computed);
        await Assert.That(result.Report.Disposition).IsEqualTo("emitted");
        await Assert.That(result.Report.Reason).IsEqualTo("safe integer macro expression");
    }

    [Test]
    public async Task Classify_Should_Emit_Signed_Shift_Expression_As_Int_When_Value_Fits_Int()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDL_AUDIO_MASK_SIGNED", "(1 << 15)"));

        await Assert.That(result.Constant).IsNotNull();
        await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("int");
        await Assert.That(result.Constant.Value).IsEqualTo("32768");
        await Assert.That(result.Constant.Kind).IsEqualTo(ConstantKind.Computed);
    }

    [Test]
    public async Task Classify_Should_Emit_Parenthesized_Hex_Literal_As_Uint()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDL_WINDOWPOS_UNDEFINED_MASK", "(0x1FFF0000u)"));

        await Assert.That(result.Constant).IsNotNull();
        await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("uint");
        await Assert.That(result.Constant.Value).IsEqualTo("0x1FFF0000u");
        await Assert.That(result.Constant.Kind).IsEqualTo(ConstantKind.Computed);
    }

    [Test]
    public async Task Classify_Should_Report_Unsupported_Expression_When_Value_Exceeds_32Bit_Macro_Width()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDL_LARGE_FIXTURE", "(1u << 40)"));

        await Assert.That(result.Constant).IsNull();
        await Assert.That(result.Report.Disposition).IsEqualTo("unsupported");
        await Assert.That(result.Report.Reason).IsEqualTo("unsupported macro expression");
    }

    private static MacroConstantCandidate Candidate(string name, string value) =>
        new(name, value, [], [], "C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/SDL_fixture.h", "SDL_fixture.h", "Neutral");
}
