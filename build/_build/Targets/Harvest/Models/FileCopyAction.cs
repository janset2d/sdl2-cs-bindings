using Cake.Core.IO;

namespace Build.Targets.Harvest.Models;

/// <summary>
/// Represents a direct file copy operation.
/// </summary>
/// <param name="SourcePath">The source file path.</param>
/// <param name="TargetPath">The target file path.</param>
/// <param name="PackageName">The name of the package this artifact belongs to.</param>
/// <param name="Origin">The origin type of the artifact.</param>
public sealed record FileCopyAction(FilePath SourcePath, FilePath TargetPath, string PackageName, ArtifactOrigin Origin) : DeploymentAction;
