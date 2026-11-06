namespace Build.Tests.Fixtures;

public sealed record TargetRunResult(
    bool Success,
    Exception? Exception,
    TestLog Log
);
