namespace Build.Host.Processes;

public sealed record HostProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string?>? Environment = null,
    string? RawArguments = null);
