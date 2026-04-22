namespace Build.Infrastructure.Tools.NativeSmoke;

/// <summary>
/// Captures native-smoke process output and exit code.
/// </summary>
public sealed record NativeSmokeRunnerResult(
    int ExitCode,
    IReadOnlyList<string> StandardOutput,
    IReadOnlyList<string> StandardError);
