using Cake.Core.IO;

namespace Build.Domain.Preflight.Models;

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

public enum UpstreamVersionAlignmentCheckStatus
{
    /// <summary>Family version major/minor is aligned to manifest upstream major/minor.</summary>
    Match,

    /// <summary>No --family-version was supplied; check is informational and skipped.</summary>
    SkippedNoFamilyVersion,

    /// <summary>
    /// A single --family-version was supplied for multiple families; strict minor alignment is
    /// deferred until per-family overrides are introduced.
    /// </summary>
    SkippedMinorAlignmentForMultiFamilyPack,

    /// <summary>--family-version could not be parsed as NuGet SemVer.</summary>
    InvalidFamilyVersion,

    /// <summary>Family identifier requested by --family is not present in manifest package_families.</summary>
    FamilyNotFound,

    /// <summary>Family library_ref does not resolve to a manifest library_manifests entry.</summary>
    LibraryRefNotFound,

    /// <summary>Manifest vcpkg_version is not parseable semantic version.</summary>
    InvalidUpstreamVersion,

    /// <summary>Family version major/minor does not match upstream major/minor (or major-only in multi-family mode).</summary>
    VersionMismatch,
}

public sealed record UpstreamVersionAlignmentCheck(
    string FamilyIdentifier,
    string? LibraryRef,
    string? FamilyVersion,
    string? UpstreamVersion,
    UpstreamVersionAlignmentCheckStatus Status,
    string? ErrorMessage)
{
    public bool IsError => Status is
        UpstreamVersionAlignmentCheckStatus.InvalidFamilyVersion or
        UpstreamVersionAlignmentCheckStatus.FamilyNotFound or
        UpstreamVersionAlignmentCheckStatus.LibraryRefNotFound or
        UpstreamVersionAlignmentCheckStatus.InvalidUpstreamVersion or
        UpstreamVersionAlignmentCheckStatus.VersionMismatch;
}

public sealed record UpstreamVersionAlignmentValidation(IReadOnlyList<UpstreamVersionAlignmentCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);

    public int CheckedFamilies => Checks
        .Select(check => check.FamilyIdentifier)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
