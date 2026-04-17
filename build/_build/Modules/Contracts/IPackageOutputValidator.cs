using Build.Context.Models;
using Build.Modules.Packaging.Models;
using Build.Modules.Packaging.Results;

namespace Build.Modules.Contracts;

public interface IPackageOutputValidator
{
    /// <summary>
    /// Runs the post-S1 nuspec guardrails (G21-G27) against the packed artifacts of a family.
    /// Every guardrail is evaluated and the aggregated <see cref="PackageValidation"/> is
    /// returned regardless of outcome, so operators see the complete failure set (not only
    /// the first tripped guardrail).
    /// </summary>
    Task<PackageValidationResult> ValidateAsync(
        PackageFamilyConfig family,
        PackageArtifacts artifacts,
        string expectedVersion,
        string expectedCommitSha,
        ProjectMetadata managedProjectMetadata);
}
