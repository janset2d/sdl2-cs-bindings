using Build.Data.Manifest;
using Build.Data.Manifest.Models;
using Build.Validation.Models;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Validation.Vcpkg;

/// <summary>
/// Cross-checks every overlay port's declared <c>vcpkg.json</c> version + port-version
/// against the upstream port's <c>vcpkg.json</c> at the current <c>external/vcpkg</c>
/// submodule pin. Catches the drift class where the vcpkg baseline advances upstream
/// but an overlay still pins the previous version — vcpkg silently honours the overlay
/// version, leaving the gap undetectable except by hand-diff.
/// </summary>
public interface IOverlayPortVersionCoherenceValidator
{
    OverlayPortVersionCoherenceValidation Validate(DirectoryPath overlayRoot, DirectoryPath upstreamPortsRoot);
}

/// <inheritdoc cref="IOverlayPortVersionCoherenceValidator"/>
public sealed class OverlayPortVersionCoherenceValidator(ICakeContext context, IVcpkgManifestRepository manifestRepository) : IOverlayPortVersionCoherenceValidator
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly IVcpkgManifestRepository _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));

    public OverlayPortVersionCoherenceValidation Validate(DirectoryPath overlayRoot, DirectoryPath upstreamPortsRoot)
    {
        ArgumentNullException.ThrowIfNull(overlayRoot);
        ArgumentNullException.ThrowIfNull(upstreamPortsRoot);

        var checks = new List<OverlayPortVersionCheck>();

        if (!_context.DirectoryExists(overlayRoot))
        {
            // No overlay-ports/ directory — nothing to validate; valid no-op.
            return new OverlayPortVersionCoherenceValidation(checks);
        }

        var overlayDirs = _context.GetSubDirectories(overlayRoot)
            .OrderBy(dir => dir.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var overlayDir in overlayDirs)
        {
            var portName = overlayDir.GetDirectoryName();
            checks.Add(CheckPort(portName, overlayDir, upstreamPortsRoot));
        }

        return new OverlayPortVersionCoherenceValidation(checks);
    }

    private OverlayPortVersionCheck CheckPort(string portName, DirectoryPath overlayDir, DirectoryPath upstreamPortsRoot)
    {
        var overlayManifestPath = overlayDir.CombineWithFilePath("vcpkg.json");
        var upstreamManifestPath = upstreamPortsRoot.Combine(portName).CombineWithFilePath("vcpkg.json");

        if (!_context.FileExists(overlayManifestPath))
        {
            return Failure(portName, OverlayPortVersionCheckStatus.OverlayManifestMissing,
                $"[G60] overlay port '{portName}' has no vcpkg.json at '{overlayManifestPath.FullPath}'. " +
                "Every overlay-ports/<port>/ directory must declare its version + port-version.");
        }

        if (!_context.FileExists(upstreamManifestPath))
        {
            return Failure(portName, OverlayPortVersionCheckStatus.UpstreamPortMissing,
                $"[G60] overlay port '{portName}' has no upstream counterpart at '{upstreamManifestPath.FullPath}'. " +
                "Did the vcpkg submodule reorganize? Or is this a Janset-only port that needs documenting?");
        }

        VcpkgManifest overlay;
        VcpkgManifest upstream;
        try
        {
            overlay = _manifestRepository.Load(overlayManifestPath);
            upstream = _manifestRepository.Load(upstreamManifestPath);
        }
        catch (CakeException ex)
        {
            return Failure(portName, OverlayPortVersionCheckStatus.InvalidJson,
                $"[G60] overlay port '{portName}' or upstream vcpkg.json failed to parse: {ex.Message}.");
        }

        return Compare(portName, overlay, upstream);
    }

    private static OverlayPortVersionCheck Failure(string portName, OverlayPortVersionCheckStatus status, string errorMessage) =>
        new(
            PortName: portName,
            OverlayVersion: null,
            OverlayPortVersion: null,
            UpstreamVersion: null,
            UpstreamPortVersion: null,
            Status: status,
            ErrorMessage: errorMessage);

    private static OverlayPortVersionCheck Compare(string portName, VcpkgManifest overlay, VcpkgManifest upstream)
    {
        var overlayPortVersion = overlay.PortVersion ?? 0;
        var upstreamPortVersion = upstream.PortVersion ?? 0;
        var versionMismatch = !string.Equals(overlay.Version, upstream.Version, StringComparison.Ordinal);
        var portVersionMismatch = overlayPortVersion != upstreamPortVersion;

        if (versionMismatch || portVersionMismatch)
        {
            return new OverlayPortVersionCheck(
                PortName: portName,
                OverlayVersion: overlay.Version,
                OverlayPortVersion: overlayPortVersion,
                UpstreamVersion: upstream.Version,
                UpstreamPortVersion: upstreamPortVersion,
                Status: OverlayPortVersionCheckStatus.VersionDrift,
                ErrorMessage:
                $"[G60] overlay port '{portName}' version '{overlay.Version}#{overlayPortVersion}' does not match " +
                $"upstream '{upstream.Version}#{upstreamPortVersion}' at the current vcpkg submodule pin. " +
                "Re-sync the overlay against external/vcpkg/ports/" + portName + "/ per " +
                "vcpkg-overlay-ports/README.md §'Maintenance Rules'.");
        }

        return new OverlayPortVersionCheck(
            PortName: portName,
            OverlayVersion: overlay.Version,
            OverlayPortVersion: overlayPortVersion,
            UpstreamVersion: upstream.Version,
            UpstreamPortVersion: upstreamPortVersion,
            Status: OverlayPortVersionCheckStatus.Match,
            ErrorMessage: null);
    }
}
