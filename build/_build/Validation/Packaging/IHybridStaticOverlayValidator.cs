using System.Collections.Immutable;
using Build.Data.Manifest.Models;
using Build.Results;

namespace Build.Validation.Packaging;
/// <summary>
/// Validates that every runtime entry in <c>manifest.json</c> uses a hybrid overlay triplet:
/// the triplet name ends with <c>-hybrid</c> AND a corresponding overlay <c>.cmake</c> file
/// exists at <c>vcpkg-overlay-triplets/{triplet}.cmake</c>. PreFlight guardrail G16.
/// </summary>
/// <remarks>
/// The hybrid-static packaging model is encoded in the overlay triplet itself; this validator
/// catches manifest typos (wrong suffix) and missing overlay drops (declared triplet has no
/// backing <c>.cmake</c> file).
/// </remarks>
public interface IHybridStaticOverlayValidator
{
    public ValidationReport Validate(IImmutableList<RuntimeInfo> runtimes);
}
