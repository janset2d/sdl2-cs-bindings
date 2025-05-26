using Cake.Core.IO;

namespace Build.Modules.Harvesting.Models;

/// <summary>
/// Base record for any action to be performed during artifact deployment.
/// </summary>
public abstract record DeploymentAction;

/// <summary>
/// Represents a direct file copy operation.
/// </summary>
/// <param name="SourcePath">The source file path.</param>
/// <param name="TargetPath">The target file path.</param>
/// <param name="PackageName">The name of the package this artifact belongs to.</param>
/// <param name="Origin">The origin type of the artifact.</param>
public sealed record FileCopyAction(FilePath SourcePath, FilePath TargetPath, string PackageName, ArtifactOrigin Origin) : DeploymentAction;

/// <summary>
/// Details of an item to be included in an archive.
/// </summary>
/// <param name="SourcePath">The absolute source path of the file.</param>
/// <param name="PackageName">The name of the package this artifact belongs to.</param>
/// <param name="Origin">The origin type of the artifact.</param>
public sealed record ArchivedItemDetails(FilePath SourcePath, string PackageName, ArtifactOrigin Origin);

/// <summary>
/// Represents an operation to create an archive (e.g., .tar.gz).
/// </summary>
/// <param name="ArchivePath">The full path where the archive file will be created.</param>
/// <param name="BaseDirectory">The working directory from which an archiver (e.g., tar) should operate. Paths for items to archive will be relative to this.</param>
/// <param name="ItemsToArchive">A list of items to include in the archive.</param>
/// <param name="ArchiveName">The name of the archive file (for logging/identification).</param>
public sealed record ArchiveCreationAction(FilePath ArchivePath, DirectoryPath BaseDirectory, IReadOnlyList<ArchivedItemDetails> ItemsToArchive, string ArchiveName)
    : DeploymentAction;

/// <summary>
/// Represents a file with its package and deployment location information.
/// </summary>
/// <param name="FilePath">The file path.</param>
/// <param name="PackageName">The package that owns this file.</param>
/// <param name="DeploymentLocation">Where this file will be deployed.</param>
public sealed record FileDeploymentInfo(FilePath FilePath, string PackageName, DeploymentLocation DeploymentLocation);

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

/// <summary>
/// Indicates how files are being deployed.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>Files are copied directly to the target directory (Windows).</summary>
    DirectCopy,
    /// <summary>Files are packaged into an archive (Unix systems).</summary>
    Archive
}

/// <summary>
/// Indicates where a specific file will be deployed.
/// </summary>
public enum DeploymentLocation
{
    /// <summary>File is copied directly to the filesystem.</summary>
    FileSystem,
    /// <summary>File is packaged into an archive.</summary>
    Archive,
}

/// <summary>
/// Specifies the origin of an artifact during the deployment process.
/// </summary>
public enum ArtifactOrigin
{
    Primary,
    Runtime,
    License,
}

/// <summary>
/// Represents the complete plan for deploying artifacts, consisting of a series of actions and metadata.
/// </summary>
/// <param name="Actions">The list of deployment actions to execute.</param>
/// <param name="Statistics">Rich metadata about what's being deployed.</param>
public sealed record DeploymentPlan(IReadOnlyList<DeploymentAction> Actions, DeploymentStatistics Statistics);
