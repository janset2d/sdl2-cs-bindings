namespace Build.Tools.ClangSharp;

public sealed record ClangSharpResult(
    int ExitCode,
    IReadOnlyList<string> StandardOutput,
    IReadOnlyList<string> StandardError);
