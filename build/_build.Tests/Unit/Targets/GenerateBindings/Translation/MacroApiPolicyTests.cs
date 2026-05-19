using Build.Targets.GenerateBindings.Translation;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class MacroApiPolicyTests
{
    [Test]
    public async Task Classify_Should_Accept_Object_Like_Sdl_Macro_From_Sdl2_Header()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\"", "SDL_hints.h"));

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.Candidate);
        await Assert.That(decision.Reason).IsEqualTo("object-like SDL macro");
    }

    [Test]
    public async Task Classify_Should_Report_Function_Like_Macro_As_Unsupported()
    {
        var candidate = Candidate("SDL_UNCLASSIFIED_FUNCTION_MACRO", "(1u << ((X) - 1))", "SDL_mouse.h", parameters: ["X"]);

        var decision = MacroApiPolicy.Classify(candidate);

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.Unsupported);
        await Assert.That(decision.Reason).IsEqualTo("function-like macro");
    }

    [Test]
    public async Task Classify_Should_Report_Public_Helper_Macro_As_Helper_Candidate()
    {
        var candidate = Candidate("SDL_VERSION_ATLEAST", "(SDL_COMPILEDVERSION >= SDL_VERSIONNUM(X, Y, Z))", "SDL_version.h", parameters: ["X", "Y", "Z"]);

        var decision = MacroApiPolicy.Classify(candidate);

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.HelperCandidate);
        await Assert.That(decision.Reason).IsEqualTo("public helper macro");
    }

    [Test]
    public async Task Classify_Should_Report_Include_Guard_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_hints_h_", string.Empty, "SDL_hints.h"));

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
        await Assert.That(decision.Reason).IsEqualTo("include guard or empty macro");
    }

    [Test]
    public async Task Classify_Should_Report_Platform_Control_Define_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_PLATFORM_WINDOWS", "1", "SDL_platform.h"));

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
        await Assert.That(decision.Reason).IsEqualTo("platform control macro");
    }

    [Test]
    public async Task Classify_Should_Treat_SDL_WINAPI_FAMILY_PHONE_As_Platform_Control_Macro()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_WINAPI_FAMILY_PHONE", "2", "SDL_platform.h"));

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
        await Assert.That(decision.Reason).IsEqualTo("platform control macro");
    }

    [Test]
    public async Task Classify_Should_Report_Sdl_Config_Build_Toggle_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_VIDEO_DRIVER_X11", "1", "SDL_config.h"));

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
        await Assert.That(decision.Reason).IsEqualTo("SDL build-time configuration macro");
    }

    [Test]
    public async Task Classify_Should_Report_Platform_Sdl_Config_Build_Toggle_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_VIDEO_DRIVER_WINDOWS", "1", "SDL_config_windows.h"));

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
        await Assert.That(decision.Reason).IsEqualTo("SDL build-time configuration macro");
    }

    [Test]
    public async Task Classify_Should_Report_Cacheline_Size_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_CACHELINE_SIZE", "128", "SDL_cpuinfo.h"));

        await AssertNonApi(decision, "C-only cache-line padding macro");
    }

    [Test]
    public async Task Classify_Should_Report_Assert_Level_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_ASSERT_LEVEL", "1", "SDL_assert.h"));

        await AssertNonApi(decision, "SDL C assertion build-time macro");
    }

    [Test]
    public async Task Classify_Should_Report_Null_While_Loop_Condition_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_NULL_WHILE_LOOP_CONDITION", "0", "SDL_assert.h"));

        await AssertNonApi(decision, "SDL C assertion helper macro");
    }

    [Test]
    public async Task Classify_Should_Report_Printf_Format_Macro_As_Non_Api()
    {
        var unsignedDecision = MacroApiPolicy.Classify(Candidate("SDL_PRIu64", "\"I64u\"", "SDL_stdinc.h"));
        var signedDecision = MacroApiPolicy.Classify(Candidate("SDL_PRIs64", "\"I64d\"", "SDL_stdinc.h"));

        await AssertNonApi(unsignedDecision, "C printf format macro");
        await AssertNonApi(signedDecision, "C printf format macro");
    }

    [Test]
    public async Task Classify_Should_Not_Report_Private_Header_Macro_As_Printf_Format()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_PRIVATE_HEADER_SWITCH", string.Empty, "SDL_internal.h"));

        await AssertNonApi(decision, "include guard or empty macro");
    }

    [Test]
    public async Task Classify_Should_Report_Revision_Macros_As_Non_Api()
    {
        var numberDecision = MacroApiPolicy.Classify(Candidate("SDL_REVISION_NUMBER", "0", "SDL_revision.h"));
        var revisionDecision = MacroApiPolicy.Classify(Candidate("SDL_REVISION", "\"hg-0:000000000000\"", "SDL_revision.h"));

        await AssertNonApi(numberDecision, "obsolete SDL revision macro");
        await AssertNonApi(revisionDecision, "obsolete SDL revision macro");
    }

    [Test]
    public async Task Classify_Should_Report_Compile_Time_Assert_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate(
            "SDL_COMPILE_TIME_ASSERT",
            "typedef int SDL_compile_time_assert_ ## name[(x) * 2 - 1]",
            "SDL_assert.h",
            parameters: ["name", "x"]));

        await AssertNonApi(decision, "C compile-time assertion macro");
    }

    [Test]
    public async Task Classify_Should_Report_Cast_Helper_Macros_As_Non_Api()
    {
        var reinterpretDecision = MacroApiPolicy.Classify(Candidate("SDL_reinterpret_cast", "(type)(expression)", "SDL_stdinc.h", parameters: ["type", "expression"]));
        var staticDecision = MacroApiPolicy.Classify(Candidate("SDL_static_cast", "(type)(expression)", "SDL_stdinc.h", parameters: ["type", "expression"]));
        var constDecision = MacroApiPolicy.Classify(Candidate("SDL_const_cast", "(type)(expression)", "SDL_stdinc.h", parameters: ["type", "expression"]));

        await AssertNonApi(reinterpretDecision, "C/C++ cast helper macro");
        await AssertNonApi(staticDecision, "C/C++ cast helper macro");
        await AssertNonApi(constDecision, "C/C++ cast helper macro");
    }

    [Test]
    public async Task Classify_Should_Report_Printf_Annotation_Macro_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_PRINTF_VARARG_FUNC", "__attribute__((format(printf, fmt, ap)))", "SDL_stdinc.h", parameters: ["fmt", "ap"]));

        await AssertNonApi(decision, "C printf annotation macro");
    }

    [Test]
    public async Task Classify_Should_Report_Non_Sdl_Name_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate("NOT_SDL_VALUE", "7", "SDL_hints.h"));

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
        await Assert.That(decision.Reason).IsEqualTo("non-SDL macro");
    }

    private static MacroConstantCandidate Candidate(string name, string value, string header, IReadOnlyList<string>? parameters = null) =>
        new(name, value, parameters ?? [], [], $"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{header}", header, "Neutral");

    private static async Task AssertNonApi(MacroApiDecision decision, string reason)
    {
        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
        await Assert.That(decision.Reason).IsEqualTo(reason);
    }
}
