using Build.Data.Manifest.Models;
using Build.Data.NativePackageMetadata;
using Build.Host.Paths;

namespace Build.Targets.Package.Services;

/// <inheritdoc />
public sealed class NativePackageMetadataGenerator(IPathService pathService, INativePackageMetadataRepository nativePackageMetadataRepository) : INativePackageMetadataGenerator
{
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly INativePackageMetadataRepository _nativePackageMetadataRepository = nativePackageMetadataRepository
        ?? throw new ArgumentNullException(nameof(nativePackageMetadataRepository));

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

        var library = manifestConfig.LibraryManifests.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Cannot generate native metadata for family '{family.Name}' because library_ref '{family.LibraryRef}' was not found in manifest library_manifests[].");

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
        await _nativePackageMetadataRepository.WriteAsync(targetPath, metadata, ct);

        ct.ThrowIfCancellationRequested();
    }
}
