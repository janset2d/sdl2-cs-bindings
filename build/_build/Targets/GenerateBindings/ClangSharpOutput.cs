namespace Build.Targets.GenerateBindings;

internal sealed record ClangSharpOutput(
    bool Accepted,
    bool Failed,
    bool Empty,
    bool NoOp,
    IReadOnlyList<string> Macros,
    IReadOnlyList<string> Diagnostics);
