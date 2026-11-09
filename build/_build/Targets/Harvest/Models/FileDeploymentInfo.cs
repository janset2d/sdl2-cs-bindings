using Cake.Core.IO;

namespace Build.Targets.Harvest.Models;

/// <summary>
/// Represents a file with its package and deployment location information.
/// </summary>
/// <param name="FilePath">The file path.</param>
/// <param name="PackageName">The package that owns this file.</param>
/// <param name="DeploymentLocation">Where this file will be deployed.</param>
public sealed record FileDeploymentInfo(FilePath FilePath, string PackageName, DeploymentLocation DeploymentLocation);
