using Build.Shared.Manifest;
using Build.Validation.Models;
using Build.Versioning;

namespace Build.Validation.Versioning;

/// <summary>
/// Scope-contains implementation of cross-family dependency resolvability (release-guardrails [G58]).
/// Feed probing is a separate optional surface; the current validator only reasons about
/// the supplied version set and manifest dependency graph.
/// </summary>
public sealed class CrossFamilyDependencyResolvabilityValidator : ICrossFamilyDependencyResolvabilityValidator
{
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
