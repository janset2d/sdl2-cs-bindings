using Build.Shared.Results;

namespace Build.Shared.Packaging;

/// <summary>
/// Module-level base for Packaging domain errors. Mirrors <c>HarvestingError</c>
/// and <c>PreflightError</c> so every build-host module exposes the same
/// <see cref="BuildError"/>-derived shape.
/// </summary>
public abstract class PackagingError : BuildError
{
    protected PackagingError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
