namespace Build.Data.ProjectMetadata;

/// <summary>
/// Evaluated MSBuild metadata for a csproj file, resolved after the full
/// Directory.Build.props chain has been applied. Package validation uses this
/// to replace hard-coded expectations with values sourced from the repository's
/// canonical props.
/// </summary>
public sealed record EvaluatedProjectMetadata(IReadOnlyList<string> TargetFrameworks, string Authors, string PackageLicenseFile, string PackageIcon);
