namespace Build.Validation.Models;

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

public sealed record VersionConsistencyValidation(IReadOnlyList<LibraryVersionCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);

    public int CheckedLibraries => Checks.Count;
}
