using Build.Context.Models;
using Build.Domain.Preflight.Results;
using Cake.Core.IO;

namespace Build.Domain.Preflight;

public interface ICsprojPackContractValidator
{
    /// <summary>
    /// Validates that every managed and native csproj referenced by <c>manifest.json package_families[]</c>
    /// conforms to the canonical pack contract: csproj structural shape (G1-G3), MinVer tag prefix
    /// coherence (G4), MSBuild family-version property naming (G5), PackageId convention (G6), Native
    /// ProjectReference path coherence (G7), family-version property sentinel fallback (G8), and
    /// cross-section family/library reference integrity (G17, G18).
    /// </summary>
    /// <param name="manifest">Loaded manifest configuration.</param>
    /// <param name="repoRoot">Repository root directory; csproj relative paths are resolved against this.</param>
    /// <returns>Pass/fail result with per-check details.</returns>
    CsprojPackContractResult Validate(ManifestConfig manifest, DirectoryPath repoRoot);
}
