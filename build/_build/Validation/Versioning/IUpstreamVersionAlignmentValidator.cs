using Build.Shared.Manifest;
using Build.Validation.Models;
using Build.Versioning;

namespace Build.Validation.Versioning;

/// <summary>
/// Upstream version alignment validator (release-guardrails [G54]). Every entry in the
/// resolved per-family version set must align with the family's upstream library
/// major/minor from <c>manifest.json library_manifests[].vcpkg_version</c>. Strict-minor
/// alignment applies unconditionally — each set entry is an explicit per-family assertion.
/// </summary>
public interface IUpstreamVersionAlignmentValidator
{
    UpstreamVersionAlignmentValidation Validate(ManifestConfig manifestConfig, PackageFamilyVersionSet versions);
}
