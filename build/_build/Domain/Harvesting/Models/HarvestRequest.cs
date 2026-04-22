namespace Build.Domain.Harvesting.Models;

/// <summary>
/// ADR-003 §3.2 per-stage request driving <c>HarvestTaskRunner</c>. Harvest is intentionally
/// version-blind (ADR-003 §3.5 "native evidence collection"); the request carries only the
/// per-RID axis and the library-scope filter. vcpkg configuration (triplet / overlay ports)
/// is resolved from <c>ManifestConfig</c> + <c>VcpkgConfiguration</c> via DI — not duplicated
/// here.
/// </summary>
/// <param name="Rid">Target RID for this harvest invocation (e.g., <c>win-x64</c>,
/// <c>linux-arm64</c>).</param>
/// <param name="Libraries">Library identifiers to harvest; empty list means "every entry in
/// <c>manifest.library_manifests[]</c>".</param>
public sealed record HarvestRequest(string Rid, IReadOnlyList<string> Libraries);
