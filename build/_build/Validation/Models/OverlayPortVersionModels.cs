namespace Build.Validation.Models;

/// <summary>
/// Per-overlay-port result from <see cref="Vcpkg.IOverlayPortVersionCoherenceValidator"/>.
/// Captures both sides of the version comparison (overlay's declared
/// <c>vcpkg.json</c> and the upstream port's <c>vcpkg.json</c> at the pinned
/// submodule commit) so diagnostic messages can cite both numbers.
/// </summary>
public sealed record OverlayPortVersionCheck(
    string PortName,
    string? OverlayVersion,
    int? OverlayPortVersion,
    string? UpstreamVersion,
    int? UpstreamPortVersion,
    OverlayPortVersionCheckStatus Status,
    string? ErrorMessage);

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

public sealed record OverlayPortVersionCoherenceValidation(IReadOnlyList<OverlayPortVersionCheck> Checks)
{
    public bool HasErrors => Checks.Any(c => c.Status != OverlayPortVersionCheckStatus.Match);
}
