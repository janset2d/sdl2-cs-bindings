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
