using Build.Results;

namespace Build.Packaging;

/// <summary>
/// Module-level base for Packaging domain errors. Mirrors <c>HarvestingError</c>
/// so the surviving build-host modules expose the same
/// <see cref="BuildError"/>-derived shape.
/// </summary>
public abstract class PackagingError(string message, Exception? exception = null) : BuildError(message, exception);

public sealed class ProjectMetadataError(string message, string? projectPath = null, Exception? exception = null) : PackagingError(message, exception)
{
    /// <summary>
    /// The csproj that was being queried when the failure occurred (canonical full-path
    /// string, Shared no-Cake invariant), when available.
    /// </summary>
    public string? ProjectPath { get; } = projectPath;
}
