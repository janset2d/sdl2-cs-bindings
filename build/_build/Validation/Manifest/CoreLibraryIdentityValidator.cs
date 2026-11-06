using Build.Data.Manifest.Models;
using Build.Validation.Models;

namespace Build.Validation.Manifest;

/// <inheritdoc />
public sealed class CoreLibraryIdentityValidator : ICoreLibraryIdentityValidator
{
    public CoreLibraryIdentityValidation Validate(ManifestConfig manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var cores = manifest.LibraryManifests.Where(lib => lib.IsCoreLib).ToList();
        var packagingConfigCoreLibrary = manifest.PackagingConfig.CoreLibrary;

        if (cores.Count != 1)
        {
            var errorMessage = cores.Count == 0
                ? "manifest.json library_manifests[] does not declare any entry with core_lib=true; exactly one is required."
                : $"manifest.json library_manifests[] declares {cores.Count} entries with core_lib=true " +
                  $"({string.Join(", ", cores.Select(c => c.VcpkgName))}); exactly one is required.";

            return new CoreLibraryIdentityValidation(new CoreLibraryIdentityCheck(
                ManifestCoreVcpkgName: null,
                PackagingConfigCoreLibrary: packagingConfigCoreLibrary,
                CoreLibraryManifestCount: cores.Count,
                Status: CoreLibraryIdentityCheckStatus.InvalidCoreLibraryManifestCount,
                ErrorMessage: errorMessage));
        }

        var manifestCoreVcpkgName = cores[0].VcpkgName;

        if (!string.Equals(manifestCoreVcpkgName, packagingConfigCoreLibrary, StringComparison.OrdinalIgnoreCase))
        {
            var errorMessage =
                $"manifest.json core library identity drift: library_manifests[core_lib=true].vcpkg_name is '{manifestCoreVcpkgName}' " +
                $"but packaging_config.core_library is '{packagingConfigCoreLibrary}'. Align both fields to a single vcpkg package name.";

            return new CoreLibraryIdentityValidation(new CoreLibraryIdentityCheck(
                ManifestCoreVcpkgName: manifestCoreVcpkgName,
                PackagingConfigCoreLibrary: packagingConfigCoreLibrary,
                CoreLibraryManifestCount: 1,
                Status: CoreLibraryIdentityCheckStatus.PackagingConfigCoreLibraryMismatch,
                ErrorMessage: errorMessage));
        }

        return new CoreLibraryIdentityValidation(new CoreLibraryIdentityCheck(
            ManifestCoreVcpkgName: manifestCoreVcpkgName,
            PackagingConfigCoreLibrary: packagingConfigCoreLibrary,
            CoreLibraryManifestCount: 1,
            Status: CoreLibraryIdentityCheckStatus.Match,
            ErrorMessage: null));
    }
}
