using Build.Validation.Models;
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
    public OverlayPortVersionCoherenceValidation Validate(DirectoryPath overlayRoot, DirectoryPath upstreamPortsRoot);
}
