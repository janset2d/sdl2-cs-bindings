using Build.Modules.Results;

namespace Build.Modules.Packaging.Results;

/// <summary>
/// Module-level base for Packaging domain errors. Mirrors <see cref="Build.Modules.Harvesting.Results.HarvestingError"/>
/// and <see cref="Build.Modules.Preflight.Results.PreflightError"/> so every build-host module exposes
/// the same <see cref="BuildError"/>-derived shape.
/// </summary>
public abstract class PackagingError : BuildError
{
    protected PackagingError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
