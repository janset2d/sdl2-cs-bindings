using Build.Targets.GenerateBindings.Translation;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class MacroSurfaceAuditTests
{
    [Test]
    public async Task PublicExpressionConstants_Should_Include_High_Value_Sdl2_Surface()
    {
        var expressions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SDL_HAPTIC_CONSTANT"] = "(1u << 0)",
            ["SDL_HAPTIC_SINE"] = "(1u << 1)",
            ["SDL_HAPTIC_RAMP"] = "(1u << 6)",
            ["SDL_HAPTIC_PAUSE"] = "(1u << 15)",
            ["SDL_AUDIO_MASK_BITSIZE"] = "(0xFF)",
            ["SDL_AUDIO_MASK_DATATYPE"] = "(1 << 8)",
            ["SDL_AUDIO_MASK_ENDIAN"] = "(1 << 12)",
            ["SDL_AUDIO_MASK_SIGNED"] = "(1 << 15)",
        };

        foreach (var expression in expressions)
        {
            var candidate = Candidate(expression.Key, expression.Value, "SDL_audit.h");
            var decision = MacroApiPolicy.Classify(candidate);
            var result = MacroValueClassifier.Classify(candidate);

            await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.Candidate);
            await Assert.That(result.Constant).IsNotNull();
            await Assert.That(result.Report.Disposition).IsEqualTo("emitted");
            await Assert.That(result.Report.Taxonomy).IsEqualTo("public-expression-constant");
        }
    }

    [Test]
    public async Task HelperMacroBacklog_Should_Track_Public_FunctionLike_Surface()
    {
        var helperBacklog = new[]
        {
            "SDL_BUTTON",
            "SDL_VERSION",
            "SDL_VERSIONNUM",
            "SDL_VERSION_ATLEAST",
            "SDL_WINDOWPOS_UNDEFINED_DISPLAY",
            "SDL_WINDOWPOS_CENTERED_DISPLAY",
            "SDL_WINDOWPOS_ISUNDEFINED",
            "SDL_WINDOWPOS_ISCENTERED",
            "SDL_DEFINE_PIXELFOURCC",
            "SDL_DEFINE_PIXELFORMAT",
            "SDL_PIXELTYPE",
            "SDL_PIXELORDER",
            "SDL_PIXELLAYOUT",
            "SDL_BITSPERPIXEL",
            "SDL_BYTESPERPIXEL",
            "SDL_ISPIXELFORMAT_INDEXED",
            "SDL_ISPIXELFORMAT_PACKED",
            "SDL_ISPIXELFORMAT_ARRAY",
            "SDL_ISPIXELFORMAT_ALPHA",
            "SDL_ISPIXELFORMAT_FOURCC",
        };

        foreach (var helper in helperBacklog)
        {
            var decision = MacroApiPolicy.Classify(Candidate(helper, "(fixture)", "SDL_audit.h", parameters: ["X"]));

            await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.HelperCandidate);
            await Assert.That(decision.Reason).IsEqualTo("public helper macro");
        }
    }

    [Test]
    public async Task NonDotNetMacroSkips_Should_Track_Known_Internal_Surface()
    {
        var skipped = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SDL_CACHELINE_SIZE"] = "SDL_cpuinfo.h",
            ["SDL_ASSERT_LEVEL"] = "SDL_assert.h",
            ["SDL_NULL_WHILE_LOOP_CONDITION"] = "SDL_assert.h",
            ["SDL_PRIu64"] = "SDL_stdinc.h",
            ["SDL_PRIs64"] = "SDL_stdinc.h",
            ["SDL_REVISION_NUMBER"] = "SDL_revision.h",
            ["SDL_REVISION"] = "SDL_revision.h",
        };

        foreach (var macro in skipped)
        {
            var decision = MacroApiPolicy.Classify(Candidate(macro.Key, "1", macro.Value));

            await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
        }
    }

    private static MacroConstantCandidate Candidate(
        string name,
        string value,
        string header,
        IReadOnlyList<string>? parameters = null) =>
        new(name, value, parameters ?? [], [], $"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{header}", header, "Neutral");
}
