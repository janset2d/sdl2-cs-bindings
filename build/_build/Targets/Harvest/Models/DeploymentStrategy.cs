namespace Build.Targets.Harvest.Models;

/// <summary>
/// Indicates how files are being deployed.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>Files are copied directly to the target directory (Windows).</summary>
    DirectCopy,
    /// <summary>Files are packaged into an archive (Unix systems).</summary>
    Archive,
}
