using Build.Context.Models;
using Build.Domain.Packaging.Models;
using NuGet.Versioning;

namespace Build.Domain.Packaging;

/// <summary>
/// Guardrail G58 — "cross-family dependency resolvability." Catches the class of misuse
/// where an operator runs <c>--target=Pack --explicit-version sdl2-image=2.8.1</c> without
/// including <c>sdl2-core</c> in the same invocation. The packed
/// <c>Janset.SDL2.Image.nupkg</c> would nuspec-emit a minimum-range dependency on
/// <c>Janset.SDL2.Core</c> that either doesn't resolve at consumer-restore time (no core
/// in scope, no core published) or points at an incoherent version.
/// <para>
/// ADR-003 §4 assigns G58 to the Pack stage. Deniz Q2 decision (Slice C direction pass,
/// 2026-04-21) adds a mirror scope-contains check in PreFlight for fail-fast ergonomics
/// before Harvest/vcpkg spins up. The validator is the same implementation in both
/// locations; Pack additionally owns the opt-in feed-probe extension (post-C.7 wiring).
/// </para>
/// </summary>
public interface IG58CrossFamilyDepResolvabilityValidator
{
    /// <summary>
    /// Validate every <c>depends_on</c> entry of every family in the supplied mapping.
    /// Per ADR-003 §2.5 (<c>depends_on</c> is ordering/consistency metadata, never auto-scope
    /// expansion), each declared cross-family dep must either be in the mapping (InScope) or
    /// flagged Missing for the caller to surface. The optional feed-probe path is introduced
    /// in a later slice's wiring.
    /// </summary>
    /// <param name="mapping">Resolved per-family version mapping (scope = <c>mapping.Keys</c>).</param>
    /// <param name="manifest">Manifest config providing <c>depends_on</c> graph.</param>
    G58CrossFamilyValidation Validate(
        IReadOnlyDictionary<string, NuGetVersion> mapping,
        ManifestConfig manifest);
}
