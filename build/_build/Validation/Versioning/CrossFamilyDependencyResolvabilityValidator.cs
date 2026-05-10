using Build.Data.Manifest;
using Build.Validation.Models;
using Build.Data.Versions;

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
    CrossFamilyDependencyValidation Validate(PackageFamilyVersionSet versions, ManifestConfig manifest);
}

/// <inheritdoc />
public sealed class CrossFamilyDependencyResolvabilityValidator : ICrossFamilyDependencyResolvabilityValidator
{
    /// <inheritdoc />
    public CrossFamilyDependencyValidation Validate(PackageFamilyVersionSet versions, ManifestConfig manifest)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(manifest);

        var checks = new List<CrossFamilyDependencyCheck>();

        foreach (var entry in versions)
        {
            var dependentFamilyName = entry.Family.Value;
            var dependentVersion = entry.Version;

            var dependentFamily = manifest.PackageFamilies.SingleOrDefault(family =>
                string.Equals(family.Name, dependentFamilyName, StringComparison.OrdinalIgnoreCase));

            if (dependentFamily is null)
            {
                checks.Add(new CrossFamilyDependencyCheck(
                    DependentFamily: dependentFamilyName,
                    DependencyFamily: dependentFamilyName,
                    ExpectedMinVersion: dependentVersion.ToNormalizedString(),
                    Status: CrossFamilyDependencyCheckStatus.Missing,
                    ErrorMessage:
                    $"[G58] family '{dependentFamilyName}' is in the resolved version set but not declared in manifest.json package_families[]. " +
                    "Either the set is malformed (rerun ResolveVersions) or the manifest is missing this family."));
                continue;
            }

            if (dependentFamily.DependsOn is null || dependentFamily.DependsOn.Count == 0)
            {
                continue;
            }

            foreach (var dependencyName in dependentFamily.DependsOn)
            {
                checks.Add(EvaluateDependency(
                    dependentFamilyName: dependentFamily.Name,
                    dependencyFamilyName: dependencyName,
                    expectedMinVersion: dependentVersion.ToNormalizedString(),
                    versions: versions));
            }
        }

        return new CrossFamilyDependencyValidation(checks);
    }

    private static CrossFamilyDependencyCheck EvaluateDependency(
        string dependentFamilyName,
        string dependencyFamilyName,
        string expectedMinVersion,
        PackageFamilyVersionSet versions)
    {
        var dependencyFamilyId = new PackageFamilyId(dependencyFamilyName);
        if (versions.Contains(dependencyFamilyId))
        {
            return new CrossFamilyDependencyCheck(
                DependentFamily: dependentFamilyName,
                DependencyFamily: dependencyFamilyName,
                ExpectedMinVersion: expectedMinVersion,
                Status: CrossFamilyDependencyCheckStatus.InScope,
                ErrorMessage: null);
        }

        return new CrossFamilyDependencyCheck(
            DependentFamily: dependentFamilyName,
            DependencyFamily: dependencyFamilyName,
            ExpectedMinVersion: expectedMinVersion,
            Status: CrossFamilyDependencyCheckStatus.Missing,
            ErrorMessage:
            $"[G58] family '{dependentFamilyName}' declares cross-family dependency on '{dependencyFamilyName}', " +
            $"but '{dependencyFamilyName}' is not in the resolved version set (scope). " +
            "Either include it in --explicit-version / --scope, or wire --feed <URL> for the Pack stage to probe the target feed for an already-published version " +
            "satisfying the lower bound (feed-probe surface is reserved for a later slice).");
    }
}
