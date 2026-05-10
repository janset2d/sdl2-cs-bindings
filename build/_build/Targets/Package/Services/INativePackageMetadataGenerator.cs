using Build.Shared.Manifest;

namespace Build.Targets.Package.Services;

public interface INativePackageMetadataGenerator
{
    Task GenerateAsync(
        PackageFamilyConfig family,
        string familyVersion,
        string buildCommitSha,
        CancellationToken ct = default);
}
