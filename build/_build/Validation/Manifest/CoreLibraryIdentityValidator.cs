using Build.Manifest;
using Build.Validation.Models;

namespace Build.Validation.Manifest;

/// <summary>
/// PreFlight guardrail G49 — core-library identity consistency.
/// <para>
/// The manifest exposes the "which vcpkg package is the core library" answer in two places:
/// <list type="number">
///   <item><description><c>library_manifests[].core_lib=true</c> (flag on a single library entry);</description></item>
///   <item><description><c>packaging_config.core_library</c> (explicit string).</description></item>
/// </list>
/// Runtime consumers (HybridStaticLeakValidator, ArtifactPlanner) read via
/// <see cref="ManifestConfig.CoreLibrary"/>, so if the two fields drift the runtime still
/// resolves to the library-flag winner. This validator surfaces the drift with a clean
/// operator-facing error before any downstream task runs.
/// </para>
/// </summary>
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
                : $"manifest.json library_manifests[] declares {cores.Count} entries with core_lib=true ({string.Join(", ", cores.Select(c => c.VcpkgName))}); exactly one is required.";

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
