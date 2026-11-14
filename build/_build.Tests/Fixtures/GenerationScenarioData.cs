namespace Build.Tests.Fixtures;

internal static class GenerationScenarioData
{
    public const string MacroWarning = "Warning (Line 86, Column 9): Function like macro definition records are not supported: 'SDL_MACRO'. Generated bindings may be incomplete.";
    public const string BuiltinWarning = "warning: undefining builtin macro [-Wbuiltin-macro-redefined]";
    public const string UnknownWarning = "warning: something else happened [-Wexample]";
    public const string Fatal = "fatal error: parse failed";
}
