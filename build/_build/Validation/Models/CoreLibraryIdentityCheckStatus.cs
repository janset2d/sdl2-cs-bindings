
namespace Build.Validation.Models;
public enum CoreLibraryIdentityCheckStatus
{
    /// <summary>Both manifest fields agree on the same core-library vcpkg name.</summary>
    Match,

    /// <summary>library_manifests[] contains zero or multiple core_lib=true entries (structural error).</summary>
    InvalidCoreLibraryManifestCount,

    /// <summary>packaging_config.core_library does not match library_manifests[core_lib=true].vcpkg_name.</summary>
    PackagingConfigCoreLibraryMismatch,
}
