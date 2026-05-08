using System.Collections.Immutable;
using Build.Results;
using Build.Shared.Manifest;

namespace Build.Validation.Packaging;

/// <summary>
/// PreFlight guardrail G16: every <c>manifest.runtimes[].triplet</c> ends with
/// <c>-hybrid</c> AND has a corresponding overlay <c>.cmake</c> file on disk.
/// </summary>
public interface IHybridStaticOverlayValidator
{
    ValidationReport Validate(IImmutableList<RuntimeInfo> runtimes);
}
