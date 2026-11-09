namespace Build.Targets.Harvest.Models;

/// <summary>
/// Statistics about what's being deployed for a specific library.
/// </summary>
/// <param name="LibraryName">The name of the library being deployed.</param>
/// <param name="PrimaryFiles">Files that belong to the target library itself.</param>
/// <param name="RuntimeFiles">Dependency files from other packages.</param>
/// <param name="LicenseFiles">License/copyright files.</param>
/// <param name="DeployedPackages">Packages that have files being deployed (excluding filtered core packages).</param>
/// <param name="FilteredPackages">Packages that were discovered but filtered out (e.g., core SDL2 for satellite libraries).</param>
/// <param name="DeploymentStrategy">How files are being deployed (DirectCopy, Archive, etc.).</param>
public sealed record DeploymentStatistics(
    string LibraryName,
    IReadOnlyList<FileDeploymentInfo> PrimaryFiles,
    IReadOnlyList<FileDeploymentInfo> RuntimeFiles,
    IReadOnlyList<FileDeploymentInfo> LicenseFiles,
    IReadOnlySet<string> DeployedPackages,
    IReadOnlySet<string> FilteredPackages,
    DeploymentStrategy DeploymentStrategy);
