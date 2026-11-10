namespace Build;

public sealed record ParsedArguments(
    DirectoryInfo? RepoRoot,
    string Config,
    DirectoryInfo? VcpkgDir,
    DirectoryInfo? VcpkgInstalledDir,
    IList<string> Library,
    string? Rid,
    IList<string> Dll,
    string? Suffix,
    IList<string> Scope,
    IList<string> ExplicitVersion,
    string? ExplicitVersions,
    string? VersionsFile);
