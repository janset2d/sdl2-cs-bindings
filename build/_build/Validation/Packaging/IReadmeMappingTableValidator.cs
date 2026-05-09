using Build.Results;
using Build.Shared.Manifest;
using Cake.Core.IO;

namespace Build.Validation.Packaging;

/// <summary>
/// Post-pack validator (release-guardrails [G57]) that asserts the README mapping
/// block currently matches the manifest-driven generator output. Returns
/// <see langword="null"/> when the README block is current, or a single failure
/// <see cref="ValidationCheck"/> describing the staleness.
/// </summary>
public interface IReadmeMappingTableValidator
{
    ValidationCheck? Validate(
        PackageFamilyConfig family,
        FilePath readmePath,
        ManifestConfig manifestConfig);
}
