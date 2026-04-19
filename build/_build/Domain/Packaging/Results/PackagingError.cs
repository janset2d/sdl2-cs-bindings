using Build.Domain.Harvesting.Results;
using Build.Domain.Preflight.Results;
using Build.Domain.Results;

namespace Build.Domain.Packaging.Results;

/// <summary>
/// Module-level base for Packaging domain errors. Mirrors <see cref="HarvestingError"/>
/// and <see cref="PreflightError"/> so every build-host module exposes
/// the same <see cref="BuildError"/>-derived shape.
/// </summary>
public abstract class PackagingError : BuildError
{
    protected PackagingError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
