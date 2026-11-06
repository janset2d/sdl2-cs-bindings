using Build.Data.Manifest.Models;
using Build.Results;
using Cake.Core.IO;

namespace Build.Validation.Packaging;

/// <summary>
/// Post-pack validator (G57) that asserts the README mapping block currently matches the
/// manifest-driven generator output. Normalizes line endings for a stable diff. Returns
/// <see langword="null"/> when the README block is current, or a single failure
/// <see cref="ValidationCheck"/> describing the staleness.
/// </summary>
public interface IReadmeMappingTableValidator
{
    public ValidationCheck? Validate(
        PackageFamilyConfig family,
        FilePath readmePath,
        ManifestConfig manifestConfig);
}
