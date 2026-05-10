using Build.Results;

namespace Build.Packaging;

/// <summary>
/// Module-level base for Packaging domain errors. Mirrors <c>HarvestingError</c>
/// so the surviving build-host modules expose the same
/// <see cref="BuildError"/>-derived shape.
/// </summary>
public abstract class PackagingError : BuildError
{
    protected PackagingError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
