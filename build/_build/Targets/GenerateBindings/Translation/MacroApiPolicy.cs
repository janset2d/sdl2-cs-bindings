namespace Build.Targets.GenerateBindings.Translation;

internal enum MacroApiDisposition
{
    Candidate,
    HelperCandidate,
    NonApi,
    Unsupported,
}

internal sealed record MacroApiDecision(MacroApiDisposition Disposition, string Reason);

internal static class MacroApiPolicy
{
    public static MacroApiDecision Classify(MacroConstantCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!candidate.Name.StartsWith("SDL_", StringComparison.Ordinal))
            return new MacroApiDecision(MacroApiDisposition.NonApi, "non-SDL macro");

        if (!BindableDeclarationPolicy.IsSdl2Header(candidate.SourceFile))
            return new MacroApiDecision(MacroApiDisposition.NonApi, "non-SDL2 header");

        if (IsSdlConfigHeader(candidate.SourceHeader))
            return new MacroApiDecision(MacroApiDisposition.NonApi, "SDL build-time configuration macro");

        var nonApiSdlMacro = ClassifyNonApiSdlMacro(candidate.Name);
        if (nonApiSdlMacro is not null)
            return nonApiSdlMacro;

        if (candidate.IsFunctionLike)
        {
            return PublicHelperMacroNames.Contains(candidate.Name)
                ? new MacroApiDecision(MacroApiDisposition.HelperCandidate, "public helper macro")
                : new MacroApiDecision(MacroApiDisposition.Unsupported, "function-like macro");
        }

        if (string.IsNullOrWhiteSpace(candidate.Value) || candidate.Name.EndsWith("_H_", StringComparison.OrdinalIgnoreCase))
            return new MacroApiDecision(MacroApiDisposition.NonApi, "include guard or empty macro");

        if (candidate.Name.StartsWith("SDL_PLATFORM_", StringComparison.Ordinal))
            return new MacroApiDecision(MacroApiDisposition.NonApi, "platform control macro");

        if (candidate.Name is "SDL_DECLSPEC" or "SDL_FORCE_INLINE" or "SDL_INLINE" or "SDL_UNUSED")
            return new MacroApiDecision(MacroApiDisposition.NonApi, "compiler control macro");

        return new MacroApiDecision(MacroApiDisposition.Candidate, "object-like SDL macro");
    }

    private static readonly HashSet<string> PublicHelperMacroNames = new(StringComparer.Ordinal)
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

    private static readonly HashSet<string> PrintfFormatMacroNames = new(StringComparer.Ordinal)
    {
        "SDL_PRIs64",
        "SDL_PRIu64",
        "SDL_PRIx64",
        "SDL_PRIX64",
        "SDL_PRIs32",
        "SDL_PRIu32",
        "SDL_PRIx32",
        "SDL_PRIX32",
    };

    private static bool IsSdlConfigHeader(string sourceHeader) =>
        string.Equals(sourceHeader, "SDL_config.h", StringComparison.Ordinal)
        || (sourceHeader.StartsWith("SDL_config_", StringComparison.Ordinal)
            && sourceHeader.EndsWith(".h", StringComparison.Ordinal));

    private static MacroApiDecision? ClassifyNonApiSdlMacro(string name) =>
        name switch
        {
            "SDL_CACHELINE_SIZE" => NonApi("C-only cache-line padding macro"),
            "SDL_ASSERT_LEVEL" => NonApi("SDL C assertion build-time macro"),
            "SDL_NULL_WHILE_LOOP_CONDITION" => NonApi("SDL C assertion helper macro"),
            "SDL_WINAPI_FAMILY_PHONE" => NonApi("platform control macro"),
            "SDL_PRINTF_VARARG_FUNC" => NonApi("C printf annotation macro"),
            _ when PrintfFormatMacroNames.Contains(name) => NonApi("C printf format macro"),
            "SDL_REVISION_NUMBER" or "SDL_REVISION" => NonApi("obsolete SDL revision macro"),
            "SDL_COMPILE_TIME_ASSERT" => NonApi("C compile-time assertion macro"),
            "SDL_reinterpret_cast" or "SDL_static_cast" or "SDL_const_cast" => NonApi("C/C++ cast helper macro"),
            _ => null,
        };

    private static MacroApiDecision NonApi(string reason) =>
        new(MacroApiDisposition.NonApi, reason);
}
