using Build.Manifest;
using Build.Validation.Models;

namespace Build.Validation.Manifest;

/// <summary>
/// PreFlight guardrail: every <c>manifest.json library_manifests[].vcpkg_version</c> matches
/// the corresponding <c>vcpkg.json overrides[].version</c> entry. Catches manifest/vcpkg drift
/// before any build operation runs.
/// </summary>
public interface IVersionConsistencyValidator
{
    VersionConsistencyValidation Validate(ManifestConfig manifest, VcpkgManifest vcpkgManifest);
}
