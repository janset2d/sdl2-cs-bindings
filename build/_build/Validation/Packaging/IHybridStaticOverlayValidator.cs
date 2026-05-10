using System.Collections.Immutable;
using Build.Manifest;
using Build.Results;

namespace Build.Validation.Packaging;

/// <summary>
/// PreFlight guardrail G16: every <c>manifest.runtimes[].triplet</c> ends with
/// <c>-hybrid</c> AND has a corresponding overlay <c>.cmake</c> file on disk.
/// </summary>
public interface IHybridStaticOverlayValidator
{
    ValidationReport Validate(IImmutableList<RuntimeInfo> runtimes);
}
