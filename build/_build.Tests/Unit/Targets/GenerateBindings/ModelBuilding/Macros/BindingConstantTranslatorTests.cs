using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.ModelBuilding.Macros;
using Build.Targets.GenerateBindings.Parse;
using Build.Targets.GenerateBindings.PlatformViews;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Macros;

public sealed class BindingConstantTranslatorTests
{
    private static CppAstParseResult ParseResult(string viewName, CppCompilation compilation) =>
        new(
            new PlatformParseView(
                Name: viewName,
                Kind: PlatformConditionKind.Neutral,
                SupportedOsPlatform: null,
                Defines: [],
                Undefines: []),
            [compilation]);

    private static CppMacro Macro(string name, string value, string header)
    {
        var macro = new CppMacro(name) { Value = value };
        macro.Span = new CppSourceSpan(
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{header}", 0, 1, 1),
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{header}", 1, 1, 2));
        macro.Tokens.Add(new CppToken(CppTokenKind.Literal, value));
        return macro;
    }

    [Test]
    public async Task Translate_Should_Emit_String_Macro_As_ReadOnlySpan_Byte()
    {
        var compilation = new CppCompilation();
        compilation.Macros.Add(Macro("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\"", "SDL_hints.h"));
        var config = BindingGenerationFixture.Sdl2CoreConfig();

        var result = BindingConstantTranslator.Translate([ParseResult("Neutral", compilation)], config);

        var constant = result.Constants.Single(c => c.Name == "SDL_HINT_RENDER_DRIVER");
        await Assert.That(constant.Type.ManagedName).IsEqualTo("ReadOnlySpan<byte>");
        await Assert.That(constant.Value).IsEqualTo("\"SDL_RENDER_DRIVER\"u8");
        await Assert.That(constant.Kind).IsEqualTo(ConstantKind.Literal);
    }

    [Test]
    public async Task Translate_Should_Emit_Numeric_Macro_As_Uint()
    {
        var compilation = new CppCompilation();
        compilation.Macros.Add(Macro("SDL_INIT_VIDEO", "0x00000020u", "SDL_video.h"));
        var config = BindingGenerationFixture.Sdl2CoreConfig();

        var result = BindingConstantTranslator.Translate([ParseResult("Neutral", compilation)], config);

        var constant = result.Constants.Single(c => c.Name == "SDL_INIT_VIDEO");
        await Assert.That(constant.Type.ManagedName).IsEqualTo("uint");
        await Assert.That(constant.Value).IsEqualTo("0x00000020u");
        await Assert.That(constant.Kind).IsEqualTo(ConstantKind.Literal);
    }

    [Test]
    public async Task Translate_Should_Report_Public_FunctionLike_Macro_As_Helper_Candidate_And_Not_Emit_It()
    {
        var compilation = new CppCompilation();
        var macro = Macro("SDL_BUTTON", "(1u << ((X) - 1))", "SDL_mouse.h");
        macro.Parameters = ["X"];
        compilation.Macros.Add(macro);
        var config = BindingGenerationFixture.Sdl2CoreConfig();

        var result = BindingConstantTranslator.Translate([ParseResult("Neutral", compilation)], config);

        await Assert.That(result.Constants.Any(c => c.Name == "SDL_BUTTON")).IsFalse();
        var entry = result.Report.Entries.FirstOrDefault(e => e.Name == "SDL_BUTTON");
        await Assert.That(entry).IsNotNull();
        await Assert.That(entry!.Disposition).IsEqualTo("helper-candidate");
        await Assert.That(result.Report.UnsupportedCount).IsEqualTo(0);
    }

    [Test]
    public async Task Translate_Should_Resolve_Object_Like_Macros_That_Invoke_Known_Helper_Macros()
    {
        var compilation = new CppCompilation();
        compilation.Macros.Add(Macro("SDL_BUTTON_LEFT", "1", "SDL_mouse.h"));
        compilation.Macros.Add(FunctionLikeMacro("SDL_BUTTON", "(1u << ((X) - 1))", "SDL_mouse.h", ["X"]));
        compilation.Macros.Add(Macro("SDL_BUTTON_LMASK", "SDL_BUTTON(SDL_BUTTON_LEFT)", "SDL_mouse.h"));
        compilation.Macros.Add(Macro("SDL_WINDOWPOS_UNDEFINED_MASK", "0x1FFF0000u", "SDL_video.h"));
        compilation.Macros.Add(FunctionLikeMacro("SDL_WINDOWPOS_UNDEFINED_DISPLAY", "(SDL_WINDOWPOS_UNDEFINED_MASK | (X))", "SDL_video.h", ["X"]));
        compilation.Macros.Add(Macro("SDL_WINDOWPOS_UNDEFINED", "SDL_WINDOWPOS_UNDEFINED_DISPLAY(0)", "SDL_video.h"));
        var config = BindingGenerationFixture.Sdl2CoreConfig();

        var result = BindingConstantTranslator.Translate([ParseResult("Neutral", compilation)], config);

        await Assert.That(result.Constants.Single(c => c.Name == "SDL_BUTTON_LMASK").Value).IsEqualTo("1u");
        await Assert.That(result.Constants.Single(c => c.Name == "SDL_WINDOWPOS_UNDEFINED").Value).IsEqualTo("536805376u");
        var buttonMaskEntry = result.Report.Entries.Single(e => e.Name == "SDL_BUTTON_LMASK");
        await Assert.That(buttonMaskEntry.MacroForm).IsEqualTo("object-like");
        await Assert.That(buttonMaskEntry.Taxonomy).IsEqualTo("public-expression-constant");
        await Assert.That(buttonMaskEntry.OriginalExpression).IsEqualTo("SDL_BUTTON(SDL_BUTTON_LEFT)");
        await Assert.That(buttonMaskEntry.ComputedValue).IsEqualTo("1");
        var buttonHelperEntry = result.Report.Entries.Single(e => e.Name == "SDL_BUTTON");
        await Assert.That(buttonHelperEntry.Disposition).IsEqualTo("helper-candidate");
        await Assert.That(buttonHelperEntry.MacroForm).IsEqualTo("function-like");
        await Assert.That(buttonHelperEntry.Taxonomy).IsEqualTo("public-helper-candidate");
        await Assert.That(result.Report.Entries.Single(e => e.Name == "SDL_WINDOWPOS_UNDEFINED_DISPLAY").Disposition).IsEqualTo("helper-candidate");
        await Assert.That(result.Report.HelperDuplicateCoalescedCount).IsEqualTo(0);
    }

    [Test]
    public async Task Translate_Should_Coalesce_Compatible_Helper_Macros_Across_Parse_Views()
    {
        var neutralCompilation = new CppCompilation();
        neutralCompilation.Macros.Add(Macro("SDL_BUTTON_LEFT", "1", "SDL_mouse.h"));
        neutralCompilation.Macros.Add(FunctionLikeMacro("SDL_BUTTON", "(1u << ((X) - 1))", "SDL_mouse.h", ["X"]));
        neutralCompilation.Macros.Add(Macro("SDL_BUTTON_LMASK", "SDL_BUTTON(SDL_BUTTON_LEFT)", "SDL_mouse.h"));
        var linuxCompilation = new CppCompilation();
        linuxCompilation.Macros.Add(FunctionLikeMacro("SDL_BUTTON", "(1u << ((X) - 1))", "SDL_mouse.h", ["X"]));
        var config = BindingGenerationFixture.Sdl2CoreConfig();

        var result = BindingConstantTranslator.Translate(
            [ParseResult("Neutral", neutralCompilation), ParseResult("Linux", linuxCompilation)],
            config);

        await Assert.That(result.Constants.Single(c => c.Name == "SDL_BUTTON_LMASK").Value).IsEqualTo("1u");
        await Assert.That(result.Report.Entries.Count(e => e.Name == "SDL_BUTTON" && e.Disposition == "helper-candidate")).IsEqualTo(2);
        await Assert.That(result.Report.HelperDuplicateCoalescedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Translate_Should_Skip_Sdl_Config_Build_Toggle_Macro_And_Not_Emit_It()
    {
        var compilation = new CppCompilation();
        compilation.Macros.Add(Macro("SDL_VIDEO_DRIVER_X11", "1", "SDL_config.h"));
        var config = BindingGenerationFixture.Sdl2CoreConfig();

        var result = BindingConstantTranslator.Translate([ParseResult("Neutral", compilation)], config);

        await Assert.That(result.Constants.Any(c => c.Name == "SDL_VIDEO_DRIVER_X11")).IsFalse();
        var entry = result.Report.Entries.Single(e => e.Name == "SDL_VIDEO_DRIVER_X11");
        await Assert.That(entry.Disposition).IsEqualTo("skipped");
        await Assert.That(entry.Reason).IsEqualTo("SDL build-time configuration macro");
        await Assert.That(result.Report.SkippedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Translate_Should_Skip_C_Only_And_Obsolete_Macros_With_Reasons()
    {
        var compilation = new CppCompilation();
        compilation.Macros.Add(Macro("SDL_CACHELINE_SIZE", "128", "SDL_cpuinfo.h"));
        compilation.Macros.Add(Macro("SDL_ASSERT_LEVEL", "1", "SDL_assert.h"));
        compilation.Macros.Add(Macro("SDL_PRIu64", "\"I64u\"", "SDL_stdinc.h"));
        compilation.Macros.Add(Macro("SDL_REVISION_NUMBER", "0", "SDL_revision.h"));
        var config = BindingGenerationFixture.Sdl2CoreConfig();

        var result = BindingConstantTranslator.Translate([ParseResult("Neutral", compilation)], config);

        await AssertSkipped(result, "SDL_CACHELINE_SIZE", "C-only cache-line padding macro");
        await AssertSkipped(result, "SDL_ASSERT_LEVEL", "SDL C assertion build-time macro");
        await AssertSkipped(result, "SDL_PRIu64", "C printf format macro");
        await AssertSkipped(result, "SDL_REVISION_NUMBER", "obsolete SDL revision macro");
        await Assert.That(result.Report.SkippedCount).IsEqualTo(4);
    }

    [Test]
    public async Task Translate_Should_Set_Report_Counts_Correctly_For_Mixed_Input()
    {
        var compilation = new CppCompilation();
        compilation.Macros.Add(Macro("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\"", "SDL_hints.h"));
        compilation.Macros.Add(Macro("SDL_INIT_VIDEO", "0x00000020u", "SDL_video.h"));
        var funcLike = Macro("SDL_BUTTON", "(1u << ((X) - 1))", "SDL_mouse.h");
        funcLike.Parameters = ["X"];
        compilation.Macros.Add(funcLike);
        // config has no required_constants → manual policy adds nothing
        var config = BindingGenerationFixture.Sdl2CoreConfig();

        var result = BindingConstantTranslator.Translate([ParseResult("Neutral", compilation)], config);

        // 3 raw candidates parsed (SDL_HINT_RENDER_DRIVER, SDL_INIT_VIDEO, SDL_BUTTON)
        await Assert.That(result.Report.ParsedCount).IsEqualTo(3);
        // SDL_BUTTON is a helper candidate, so 2 object-like macros reach value classification
        await Assert.That(result.Report.CandidateCount).IsEqualTo(2);
        // 2 emitted constants
        await Assert.That(result.Report.EmittedCount).IsEqualTo(2);
        await Assert.That(result.Report.UnsupportedCount).IsEqualTo(0);
        await Assert.That(result.Report.ConflictCount).IsEqualTo(0);
    }

    [Test]
    public async Task Translate_Should_Include_Required_Constant_Not_Present_In_Source()
    {
        // SDL_INIT_TIMER lives in SDL.h which is excluded from parsing.
        // Required constant config ensures it still reaches the binding model.
        var compilation = new CppCompilation(); // no macros
        var config = BindingGenerationFixture.Sdl2CoreConfig(requiredConstants:
        [
            BindingGenerationFixture.RequiredConstant("SDL_INIT_TIMER"),
        ]);

        var result = BindingConstantTranslator.Translate([ParseResult("Neutral", compilation)], config);

        await Assert.That(result.Constants.Any(c => c.Name == "SDL_INIT_TIMER")).IsTrue();
        var entry = result.Report.Entries.Single(e => e.Name == "SDL_INIT_TIMER");
        await Assert.That(entry.Disposition).IsEqualTo("included");
    }

    [Test]
    public async Task Translate_Should_Coalesce_Compatible_Duplicates_Across_Views()
    {
        // Same macro defined in both Neutral and Linux compilations (header included in both)
        var neutralCompilation = new CppCompilation();
        neutralCompilation.Macros.Add(Macro("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\"", "SDL_hints.h"));
        var linuxCompilation = new CppCompilation();
        linuxCompilation.Macros.Add(Macro("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\"", "SDL_hints.h"));
        var config = BindingGenerationFixture.Sdl2CoreConfig();

        var result = BindingConstantTranslator.Translate(
            [ParseResult("Neutral", neutralCompilation), ParseResult("Linux", linuxCompilation)],
            config);

        await Assert.That(result.Constants.Count(c => c.Name == "SDL_HINT_RENDER_DRIVER")).IsEqualTo(1);
        await Assert.That(result.Report.DuplicateCoalescedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Translate_Should_Emit_Haptic_Shift_Macros_And_Include_Ramp()
    {
        var compilation = new CppCompilation();
        compilation.Macros.Add(Macro("SDL_HAPTIC_CONSTANT", "(1u << 0)", "SDL_haptic.h"));
        compilation.Macros.Add(Macro("SDL_HAPTIC_RAMP", "(1u << 6)", "SDL_haptic.h"));
        compilation.Macros.Add(Macro("SDL_HAPTIC_PAUSE", "(1u << 15)", "SDL_haptic.h"));
        var config = BindingGenerationFixture.Sdl2CoreConfig();

        var result = BindingConstantTranslator.Translate([ParseResult("Neutral", compilation)], config);

        await Assert.That(result.Constants.Single(c => c.Name == "SDL_HAPTIC_CONSTANT").Value).IsEqualTo("1u");
        await Assert.That(result.Constants.Single(c => c.Name == "SDL_HAPTIC_RAMP").Value).IsEqualTo("64u");
        await Assert.That(result.Constants.Single(c => c.Name == "SDL_HAPTIC_PAUSE").Value).IsEqualTo("32768u");
        await Assert.That(result.Constants.Single(c => c.Name == "SDL_HAPTIC_RAMP").Kind).IsEqualTo(ConstantKind.Computed);
        await Assert.That(result.Report.Entries.Single(e => e.Name == "SDL_HAPTIC_RAMP").Reason)
            .IsEqualTo("safe integer macro expression");
    }

    private static CppMacro FunctionLikeMacro(string name, string value, string header, IReadOnlyList<string> parameters)
    {
        var macro = Macro(name, value, header);
        macro.Parameters = parameters.ToList();
        return macro;
    }

    private static async Task AssertSkipped(BindingConstantTranslationResult result, string name, string reason)
    {
        await Assert.That(result.Constants.Any(c => c.Name == name)).IsFalse();
        var entry = result.Report.Entries.Single(e => e.Name == name);
        await Assert.That(entry.Disposition).IsEqualTo("skipped");
        await Assert.That(entry.Reason).IsEqualTo(reason);
    }
}
