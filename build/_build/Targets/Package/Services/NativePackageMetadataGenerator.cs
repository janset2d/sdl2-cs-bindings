using Build.Host.Paths;
using Build.Data.NativePackageMetadata;
using Build.Data.Manifest.Models;

namespace Build.Targets.Package.Services;

/// <summary>
/// Pack-time generator for the <c>janset-native-metadata.json</c> file embedded into every
/// <c>.Native</c> nupkg. Output is the machine-readable half of the cross-referenced metadata
/// pair (the README mapping table being the human-readable half — see <see cref="ReadmeMappingTableGenerator"/>).
/// </summary>
public interface INativePackageMetadataGenerator
{
    Task GenerateAsync(
        ManifestConfig manifestConfig,
        PackageFamilyConfig family,
        string familyVersion,
        string buildCommitSha,
        CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class NativePackageMetadataGenerator(
    IPathService pathService,
    INativePackageMetadataRepository nativePackageMetadataRepository) : INativePackageMetadataGenerator
{
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    private readonly INativePackageMetadataRepository _nativePackageMetadataRepository =
        nativePackageMetadataRepository ?? throw new ArgumentNullException(nameof(nativePackageMetadataRepository));

    public async Task GenerateAsync(
        ManifestConfig manifestConfig,
        PackageFamilyConfig family,
        string familyVersion,
        string buildCommitSha,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifestConfig);
        ArgumentNullException.ThrowIfNull(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildCommitSha);

        var library = manifestConfig.LibraryManifests.SingleOrDefault(candidate => string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (library is null)
        {
            throw new InvalidOperationException(
                $"Cannot generate native metadata for family '{family.Name}' because library_ref '{family.LibraryRef}' was not found in manifest library_manifests[].");
        }

        var triplets = manifestConfig.Runtimes
            .Select(runtime => runtime.Triplet)
            .Where(triplet => !string.IsNullOrWhiteSpace(triplet))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var metadata = new NativePackageMetadataDocument
        {
            JansetFamilyVersion = familyVersion,
            FamilyIdentifier = family.Name,
            UpstreamLibrary = library.VcpkgName,
            UpstreamVersion = library.VcpkgVersion,
            VcpkgPortVersion = library.VcpkgPortVersion,
            TripletSet = triplets,
            BuildCommit = buildCommitSha,
        };

        var targetPath = _pathService.GetHarvestLibraryNativeMetadataFile(family.LibraryRef);
        await _nativePackageMetadataRepository.WriteAsync(targetPath, metadata, ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();
    }
}
