using Build.Data.Manifest.Models;

namespace Build.Targets.Package.Services;

/// <summary>
/// Pack-time generator for the <c>janset-native-metadata.json</c> file embedded into every
/// <c>.Native</c> nupkg. Output is the machine-readable half of the cross-referenced metadata
/// pair (the README mapping table being the human-readable half — see <see cref="ReadmeMappingTableGenerator"/>).
/// </summary>
public interface INativePackageMetadataGenerator
{
    public Task GenerateAsync(
        ManifestConfig manifestConfig,
        PackageFamilyConfig family,
        string familyVersion,
        string buildCommitSha,
        CancellationToken ct = default);
}
