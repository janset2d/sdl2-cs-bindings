using Build.Context.Models;

namespace Build.Domain.Packaging;

public interface INativePackageMetadataGenerator
{
    Task GenerateAsync(
        PackageFamilyConfig family,
        string familyVersion,
        string buildCommitSha,
        CancellationToken cancellationToken = default);
}
