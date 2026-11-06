using Build.Data.Manifest.Models;
using Build.Validation.Models;

namespace Build.Validation.Manifest;
/// <summary>
/// PreFlight guardrail G49: core-library identity consistency.
/// <para>
/// The manifest exposes the "which vcpkg package is the core library" answer in two places:
/// <list type="number">
///   <item><description><c>library_manifests[].core_lib=true</c> (flag on a single library entry);</description></item>
///   <item><description><c>packaging_config.core_library</c> (explicit string).</description></item>
/// </list>
/// Runtime consumers read via <see cref="ManifestConfig.CoreLibrary"/>, so if the two fields
/// drift the runtime still resolves to the library-flag winner. This validator surfaces the
/// drift with a clean operator-facing error before any downstream task runs.
/// </para>
/// </summary>
public interface ICoreLibraryIdentityValidator
{
    public CoreLibraryIdentityValidation Validate(ManifestConfig manifest);
}
