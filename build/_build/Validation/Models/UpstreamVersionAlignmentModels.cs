namespace Build.Validation.Models;

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

    // Manifest-shape errors (duplicate names) short-circuit the validator before any per-family
    // alignment runs, so they do not represent a "checked family". Excluding them here keeps
    // the reporter's "{0} family row(s) evaluated" message accurate when the only entries are
    // the duplicate-detection rows.
    public int CheckedFamilies => Checks
        .Where(check => check.Status is not UpstreamVersionAlignmentCheckStatus.DuplicateFamilyName
                                      and not UpstreamVersionAlignmentCheckStatus.DuplicateLibraryName)
        .Select(check => check.FamilyIdentifier)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
