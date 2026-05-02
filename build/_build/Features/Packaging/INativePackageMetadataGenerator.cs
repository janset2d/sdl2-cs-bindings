using Build.Shared.Manifest;

namespace Build.Features.Packaging;

public interface INativePackageMetadataGenerator
{
    Task GenerateAsync(
        PackageFamilyConfig family,
        string familyVersion,
        string buildCommitSha,
        CancellationToken cancellationToken = default);
}
