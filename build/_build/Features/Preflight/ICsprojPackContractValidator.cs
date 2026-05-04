using Build.Shared.Manifest;
using Cake.Core.IO;

namespace Build.Features.Preflight;

public interface ICsprojPackContractValidator
{
    /// <summary>
    /// Validates that every managed and native csproj referenced by <c>manifest.json package_families[]</c>
    /// conforms to the canonical pack contract: csproj PackageId (G6), Native ProjectReference (G7),
    /// and cross-section family/library reference integrity (G17, G18).
    /// </summary>
    /// <param name="manifest">Loaded manifest configuration.</param>
    /// <param name="repoRoot">Repository root directory; csproj relative paths are resolved against this.</param>
    /// <returns>Pass/fail result with per-check details.</returns>
    CsprojPackContractResult Validate(ManifestConfig manifest, DirectoryPath repoRoot);
}
