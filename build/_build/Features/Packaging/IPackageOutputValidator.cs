using Build.Shared.Manifest;
using Build.Shared.Packaging;
using Cake.Core.IO;

namespace Build.Features.Packaging;

public interface IPackageOutputValidator
{
    /// <summary>
    /// Runs post-pack guardrails against the packed artifacts of a family.
    /// Current scope includes G21-G27 and payload/metadata/readme checks
    /// (G47, G48, G51, G55, G56, G57).
    /// Every guardrail is evaluated and the aggregated <see cref="PackageValidation"/> is
    /// returned regardless of outcome, so operators see the complete failure set (not only
    /// the first tripped guardrail).
    /// </summary>
    Task<PackageValidationResult> ValidateAsync(
        PackageFamilyConfig family,
        PackageArtifacts artifacts,
        string expectedVersion,
        string expectedCommitSha,
        ProjectMetadata managedProjectMetadata,
        ManifestConfig manifestConfig,
        FilePath readmePath);
}
