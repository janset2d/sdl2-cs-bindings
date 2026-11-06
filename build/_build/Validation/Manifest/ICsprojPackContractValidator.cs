using Build.Data.Manifest.Models;
using Build.Validation.Models;
using Cake.Core.IO;

namespace Build.Validation.Manifest;
/// <summary>
/// Validates that every managed and native csproj referenced by <c>manifest.json package_families[]</c>
/// conforms to the canonical pack contract guardrails G6, G7, G17, G18.
/// </summary>
/// <remarks>
/// Guardrails G1 (PrivateAssets="all"), G2 (paired PackageReference),
/// G3 (bracket-notation PackageVersion), G4 (MinVerTagPrefix alignment),
/// G5 (family-version property name convention), and G8
/// (sentinel fallback) were retired when within-family exact-pin was replaced with SkiaSharp-style
/// minimum range. This validator retains the structural checks that remain relevant:
/// canonical PackageId naming (G6), Native
/// ProjectReference path correctness (G7), and manifest cross-section references (G17, G18).
///
/// The validator parses each csproj as XML (no MSBuild evaluation) so it can run inside PreFlight
/// without invoking the SDK. This deliberately makes it a structural check — it cannot evaluate
/// expressions like <c>$(Version)</c>, only verify their literal presence.
/// </remarks>
public interface ICsprojPackContractValidator
{
    /// <summary>
    /// Validates that every managed and native csproj referenced by <c>manifest.json package_families[]</c>
    /// conforms to the canonical pack contract: csproj PackageId (G6), Native ProjectReference (G7),
    /// and cross-section family/library reference integrity (G17, G18).
    /// </summary>
    /// <param name="manifest">Loaded manifest configuration.</param>
    /// <param name="repoRoot">Repository root directory; csproj relative paths are resolved against this.</param>
    public CsprojPackContractValidation Validate(ManifestConfig manifest, DirectoryPath repoRoot);
}
