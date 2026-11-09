using Cake.Core.IO;

namespace Build.Targets.Harvest.Models;

/// <summary>
/// Details of an item to be included in an archive.
/// </summary>
/// <param name="SourcePath">The absolute source path of the file.</param>
/// <param name="PackageName">The name of the package this artifact belongs to.</param>
/// <param name="Origin">The origin type of the artifact.</param>
public sealed record ArchivedItemDetails(FilePath SourcePath, string PackageName, ArtifactOrigin Origin);
