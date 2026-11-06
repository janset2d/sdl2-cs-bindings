using Cake.Core.IO;

namespace Build.Targets.Harvest.Models;
/// <summary>
/// Represents an operation to create an archive (e.g., .tar.gz).
/// </summary>
/// <param name="ArchivePath">The full path where the archive file will be created.</param>
/// <param name="BaseDirectory">The working directory from which an archiver (e.g., tar) should operate. Paths for items to archive will be relative to this.</param>
/// <param name="ItemsToArchive">A list of items to include in the archive.</param>
/// <param name="ArchiveName">The name of the archive file (for logging/identification).</param>
public sealed record ArchiveCreationAction(FilePath ArchivePath, DirectoryPath BaseDirectory, IReadOnlyList<ArchivedItemDetails> ItemsToArchive, string ArchiveName)
    : DeploymentAction;
