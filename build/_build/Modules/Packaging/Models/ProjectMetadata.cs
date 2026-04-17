namespace Build.Modules.Packaging.Models;

/// <summary>
/// Evaluated MSBuild metadata for a csproj file, resolved after the full
/// Directory.Build.props chain has been applied. Used by <see
/// cref="Contracts.IPackageOutputValidator"/> to replace hard-coded
/// expectations with values sourced from the repository's canonical props.
/// </summary>
public sealed record ProjectMetadata(
    IReadOnlyList<string> TargetFrameworks,
    string Authors,
    string PackageLicenseFile,
    string PackageIcon);
