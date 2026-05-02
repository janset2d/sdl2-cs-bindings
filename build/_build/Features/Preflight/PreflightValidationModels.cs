using Cake.Core.IO;

namespace Build.Features.Preflight;

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

public enum CoreLibraryIdentityCheckStatus
{
    /// <summary>Both manifest fields agree on the same core-library vcpkg name.</summary>
    Match,

    /// <summary>library_manifests[] contains zero or multiple core_lib=true entries (structural error).</summary>
    InvalidCoreLibraryManifestCount,

    /// <summary>packaging_config.core_library does not match library_manifests[core_lib=true].vcpkg_name.</summary>
    PackagingConfigCoreLibraryMismatch,
}

public sealed record CoreLibraryIdentityCheck(
    string? ManifestCoreVcpkgName,
    string PackagingConfigCoreLibrary,
    int CoreLibraryManifestCount,
    CoreLibraryIdentityCheckStatus Status,
    string? ErrorMessage)
{
    public bool IsValid => Status == CoreLibraryIdentityCheckStatus.Match;
}

public sealed record CoreLibraryIdentityValidation(CoreLibraryIdentityCheck Check)
{
    public bool HasErrors => !Check.IsValid;
}

