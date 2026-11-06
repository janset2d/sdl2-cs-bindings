namespace Build.Validation.Models;

public sealed record CoreLibraryIdentityCheck(
    string? ManifestCoreVcpkgName,
    string PackagingConfigCoreLibrary,
    int CoreLibraryManifestCount,
    CoreLibraryIdentityCheckStatus Status,
    string? ErrorMessage)
{
    public bool IsValid => Status == CoreLibraryIdentityCheckStatus.Match;
}
