using Build.Shared.Manifest;
using Build.Validation.Models;

namespace Build.Validation.Manifest;

/// <summary>
/// PreFlight guardrail G49: <c>library_manifests[core_lib=true].vcpkg_name</c> agrees with
/// <c>packaging_config.core_library</c>. Catches drift between the two manifest fields that
/// both declare the core library identity.
/// </summary>
public interface ICoreLibraryIdentityValidator
{
    CoreLibraryIdentityValidation Validate(ManifestConfig manifest);
}
