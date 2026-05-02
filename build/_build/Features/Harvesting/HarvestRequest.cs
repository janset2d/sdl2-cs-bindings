namespace Build.Features.Harvesting;

/// <summary>
/// Request for <c>HarvestPipeline</c>.
/// Harvest is version-blind, so the request carries only the target RID and library filter.
/// vcpkg configuration is resolved separately through DI.
/// </summary>
/// <param name="Rid">Target RID for this harvest invocation (e.g., <c>win-x64</c>,
/// <c>linux-arm64</c>).</param>
/// <param name="Libraries">Library identifiers to harvest; empty list means "every entry in
/// <c>manifest.library_manifests[]</c>".</param>
public sealed record HarvestRequest(string Rid, IReadOnlyList<string> Libraries);
