using Cake.Core.IO;

namespace Build.Modules.Preflight.Models;

public enum LibraryVersionCheckStatus
{
    Match,
    MissingOverride,
    InvalidManifestVersion,
    InvalidOverrideVersion,
    VersionMismatch,
    PortVersionMismatch,
}

public sealed record LibraryVersionCheck(
    string LibraryName,
    string VcpkgName,
    string ManifestVersion,
    int ManifestPortVersion,
    string? OverrideVersion,
    int? OverridePortVersion,
    LibraryVersionCheckStatus Status)
{
    public bool IsError => Status is not LibraryVersionCheckStatus.Match and not LibraryVersionCheckStatus.MissingOverride;
}

public sealed record VersionConsistencyValidation(
    FilePath ManifestPath,
    FilePath VcpkgManifestPath,
    IReadOnlyList<LibraryVersionCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);

    public int CheckedLibraries => Checks.Count;
}

public sealed record RuntimeStrategyCheck(
    string Rid,
    string Triplet,
    string Strategy,
    bool IsValid,
    string? ResolvedModel,
    string? ErrorMessage);

public sealed record StrategyCoherenceValidation(IReadOnlyList<RuntimeStrategyCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => !check.IsValid);

    public int CheckedRuntimes => Checks.Count;
}
