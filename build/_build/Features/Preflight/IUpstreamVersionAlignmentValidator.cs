using Build.Shared.Manifest;
using NuGet.Versioning;

namespace Build.Features.Preflight;

/// <summary>
/// Post-ADR-003 G54 validator shape: accepts a resolved per-family
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> mapping rather than the legacy scalar
/// <c>(Families, FamilyVersion)</c> pair. Every mapping entry is a single-family assertion,
/// so strict-minor alignment applies unconditionally (the pre-B1 <c>requestedFamilies.Count == 1</c>
/// skip-minor-for-multi-family branch retires).
/// </summary>
public interface IUpstreamVersionAlignmentValidator
{
    UpstreamVersionAlignmentResult Validate(
        ManifestConfig manifestConfig,
        IReadOnlyDictionary<string, NuGetVersion> versions);
}
