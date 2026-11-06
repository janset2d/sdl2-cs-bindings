using Build.Data.Manifest.Models;
using Build.Data.Versions;
using Build.Validation.Models;

namespace Build.Validation.Versioning;

/// <summary>
/// Cross-family dependency resolvability validator (release-guardrails [G58]).
/// Catches cases where a selected family depends on another family that is not in the
/// current version set. Feed probing is a separate optional surface; the current validator
/// only reasons about the supplied version set and manifest dependency graph.
/// </summary>
public interface ICrossFamilyDependencyResolvabilityValidator
{
    /// <summary>
    /// Validate every <c>depends_on</c> entry of every family in the supplied set.
    /// <c>depends_on</c> does not auto-expand scope, so each declared cross-family dependency
    /// must either be in the set (<c>InScope</c>) or be reported as missing.
    /// </summary>
    /// <param name="versions">Resolved per-family version set (scope = the set's families).</param>
    /// <param name="manifest">Manifest config providing <c>depends_on</c> graph.</param>
    public CrossFamilyDependencyValidation Validate(PackageFamilyVersionSet versions, ManifestConfig manifest);
}
