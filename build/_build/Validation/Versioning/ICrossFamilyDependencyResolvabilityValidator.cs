using Build.Shared.Manifest;
using Build.Validation.Models;
using Build.Versioning;

namespace Build.Validation.Versioning;

/// <summary>
/// Cross-family dependency resolvability validator (release-guardrails [G58]).
/// Catches cases where a selected family depends on another family that is neither
/// in the current version set nor otherwise available to the caller.
/// </summary>
public interface ICrossFamilyDependencyResolvabilityValidator
{
    /// <summary>
    /// Validate every <c>depends_on</c> entry of every family in the supplied set.
    /// <c>depends_on</c> does not auto-expand scope, so each declared cross-family dependency
    /// must either be in the set (<c>InScope</c>) or be reported as missing. The optional
    /// feed-probe path extends that check when a caller wants to inspect an external feed.
    /// </summary>
    /// <param name="versions">Resolved per-family version set (scope = the set's families).</param>
    /// <param name="manifest">Manifest config providing <c>depends_on</c> graph.</param>
    CrossFamilyDependencyValidation Validate(PackageFamilyVersionSet versions, ManifestConfig manifest);
}
