using Build.Shared.Manifest;
using NuGet.Versioning;

namespace Build.Shared.Packaging;

/// <summary>
/// Guardrail G58: cross-family dependency resolvability.
/// Catches cases where a selected family depends on another family that is neither in the
/// current version mapping nor otherwise available to the caller.
/// </summary>
public interface IG58CrossFamilyDepResolvabilityValidator
{
    /// <summary>
    /// Validate every <c>depends_on</c> entry of every family in the supplied mapping.
    /// <c>depends_on</c> does not auto-expand scope, so each declared cross-family dependency
    /// must either be in the mapping (<c>InScope</c>) or be reported as missing. The optional
    /// feed-probe path extends that check when a caller wants to inspect an external feed.
    /// </summary>
    /// <param name="mapping">Resolved per-family version mapping (scope = <c>mapping.Keys</c>).</param>
    /// <param name="manifest">Manifest config providing <c>depends_on</c> graph.</param>
    G58CrossFamilyValidation Validate(
        IReadOnlyDictionary<string, NuGetVersion> mapping,
        ManifestConfig manifest);
}
