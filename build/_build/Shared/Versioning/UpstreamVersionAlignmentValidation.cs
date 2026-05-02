namespace Build.Shared.Versioning;

public enum UpstreamVersionAlignmentCheckStatus
{
    /// <summary>Family version major/minor is aligned to manifest upstream major/minor.</summary>
    Match,

    /// <summary>Family identifier in the mapping is not present in manifest package_families.</summary>
    FamilyNotFound,

    /// <summary>Manifest package_families[] contains duplicate names.</summary>
    DuplicateFamilyName,

    /// <summary>Family library_ref does not resolve to a manifest library_manifests entry.</summary>
    LibraryRefNotFound,

    /// <summary>Manifest library_manifests[] contains duplicate names.</summary>
    DuplicateLibraryName,

    /// <summary>Manifest vcpkg_version is not parseable semantic version.</summary>
    InvalidUpstreamVersion,

    /// <summary>Family version major/minor does not match upstream major/minor.</summary>
    VersionMismatch,
}

public sealed record UpstreamVersionAlignmentCheck(
    string FamilyIdentifier,
    string? LibraryRef,
    string FamilyVersion,
    string? UpstreamVersion,
    UpstreamVersionAlignmentCheckStatus Status,
    string? ErrorMessage)
{
    public bool IsError => Status is not UpstreamVersionAlignmentCheckStatus.Match;
}

public sealed record UpstreamVersionAlignmentValidation(IReadOnlyList<UpstreamVersionAlignmentCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);

    public int CheckedFamilies => Checks
        .Select(check => check.FamilyIdentifier)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
