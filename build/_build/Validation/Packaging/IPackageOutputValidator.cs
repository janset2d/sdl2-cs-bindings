using Build.Features.Packaging;
using Build.Results;
using Build.Shared.Manifest;
using Build.Shared.Packaging;
using Cake.Core.IO;

namespace Build.Validation.Packaging;

public interface IPackageOutputValidator
{
    /// <summary>
    /// Runs post-pack guardrails against the packed artifacts of a family.
    /// Current scope includes G21–G27 and payload/metadata/readme checks
    /// (G47, G48, G51, G55, G56, G57).
    /// Every guardrail is evaluated and the aggregated <see cref="ValidationReport"/> is
    /// returned regardless of outcome, so operators see the complete failure set (not only
    /// the first tripped guardrail). Each <see cref="ValidationCheck"/> carries the
    /// guardrail ID in <c>Code</c> (e.g. "G21") and a human-readable behavior label in
    /// <c>Name</c>; <c>Code</c> is null for foundational checks (NuspecLoad, ProjectMetadataComplete).
    /// </summary>
    Task<ValidationReport> ValidateAsync(
        PackageFamilyConfig family,
        PackageArtifacts artifacts,
        string expectedVersion,
        string expectedCommitSha,
        ProjectMetadata managedProjectMetadata,
        ManifestConfig manifestConfig,
        FilePath readmePath);
}
