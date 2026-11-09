namespace Build.Targets.Harvest.Models;

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
