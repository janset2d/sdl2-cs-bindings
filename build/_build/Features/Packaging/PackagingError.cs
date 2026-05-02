using Build.Features.Preflight;
using Build.Shared.Harvesting;
using Build.Shared.Results;

namespace Build.Features.Packaging;

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
