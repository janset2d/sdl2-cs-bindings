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
/// Represents the complete plan for deploying artifacts, consisting of a series of actions.
/// </summary>
/// <param name="Actions">The list of deployment actions to execute.</param>
public sealed record DeploymentPlan(IReadOnlyList<DeploymentAction> Actions);
