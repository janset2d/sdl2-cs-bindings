using Build.Context.Models;
using Build.Domain.Packaging.Models;
using NuGet.Versioning;

namespace Build.Domain.Packaging;

/// <summary>
/// Scope-contains implementation of G58 per Slice C.6. Feed-probe extension is a
/// separate surface (future <c>IG58FeedProbe</c>) that Pack stage wiring will inject
/// opt-in via <c>--feed &lt;URL&gt;</c>; not wired by Slice C.
/// </summary>
public sealed class G58CrossFamilyDepResolvabilityValidator : IG58CrossFamilyDepResolvabilityValidator
{
    public G58CrossFamilyValidation Validate(
        IReadOnlyDictionary<string, NuGetVersion> mapping,
        ManifestConfig manifest)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(manifest);

        var checks = new List<G58CrossFamilyCheck>();

        foreach (var (dependentFamilyName, dependentVersion) in mapping)
        {
            var dependentFamily = manifest.PackageFamilies.SingleOrDefault(family =>
                string.Equals(family.Name, dependentFamilyName, StringComparison.OrdinalIgnoreCase));

            if (dependentFamily is null)
            {
                // Unknown family in mapping surfaces as a Missing check keyed against itself
                // so the caller sees the manifest-coherence failure via the same mechanism.
                checks.Add(new G58CrossFamilyCheck(
                    DependentFamily: dependentFamilyName,
                    DependencyFamily: dependentFamilyName,
                    ExpectedMinVersion: dependentVersion.ToNormalizedString(),
                    Status: G58CrossFamilyCheckStatus.Missing,
                    ErrorMessage:
                    $"G58: family '{dependentFamilyName}' is in the resolved version mapping but not declared in manifest.json package_families[]. " +
                    "Either the mapping is malformed (rerun ResolveVersions) or the manifest is missing this family."));
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
                    mapping: mapping));
            }
        }

        return new G58CrossFamilyValidation(checks);
    }

    private static G58CrossFamilyCheck EvaluateDependency(
        string dependentFamilyName,
        string dependencyFamilyName,
        string expectedMinVersion,
        IReadOnlyDictionary<string, NuGetVersion> mapping)
    {
        if (mapping.ContainsKey(dependencyFamilyName))
        {
            return new G58CrossFamilyCheck(
                DependentFamily: dependentFamilyName,
                DependencyFamily: dependencyFamilyName,
                ExpectedMinVersion: expectedMinVersion,
                Status: G58CrossFamilyCheckStatus.InScope,
                ErrorMessage: null);
        }

        return new G58CrossFamilyCheck(
            DependentFamily: dependentFamilyName,
            DependencyFamily: dependencyFamilyName,
            ExpectedMinVersion: expectedMinVersion,
            Status: G58CrossFamilyCheckStatus.Missing,
            ErrorMessage:
            $"G58: family '{dependentFamilyName}' declares cross-family dependency on '{dependencyFamilyName}', " +
            $"but '{dependencyFamilyName}' is not in the resolved version mapping (scope). " +
            "Either include it in --explicit-version / --scope, or wire --feed <URL> for the Pack stage to probe the target feed for an already-published version " +
            "satisfying the lower bound (feed-probe surface is reserved for a later slice).");
    }
}
