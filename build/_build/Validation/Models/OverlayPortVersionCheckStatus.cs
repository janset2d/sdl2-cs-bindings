
namespace Build.Validation.Models;
public enum OverlayPortVersionCheckStatus
{
    /// <summary>Overlay <c>version</c> + <c>port-version</c> match upstream at the pinned commit.</summary>
    Match,

    /// <summary>Overlay <c>version</c> or <c>port-version</c> diverges from upstream — drift to resolve.</summary>
    VersionDrift,

    /// <summary>Overlay directory exists but carries no <c>vcpkg.json</c>.</summary>
    OverlayManifestMissing,

    /// <summary>Overlay port has no upstream counterpart under <c>external/vcpkg/ports/&lt;port&gt;/</c>.</summary>
    UpstreamPortMissing,

    /// <summary>Either side's <c>vcpkg.json</c> failed to parse as JSON.</summary>
    InvalidJson,
}
