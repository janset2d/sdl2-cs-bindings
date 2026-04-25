namespace Build.Domain.Harvesting.Models;

/// <summary>
/// Request for <c>NativeSmokeTaskRunner</c>.
/// NativeSmoke validates the harvested native payload for a single RID. Harvest output and
/// CMake preset paths come from <c>IPathService</c>, so the request only needs the RID.
/// </summary>
/// <param name="Rid">Target RID whose <c>tests/smoke-tests/native-smoke/CMakePresets.json</c>
/// entry is driven (e.g., <c>win-x64</c>).</param>
public sealed record NativeSmokeRequest(string Rid);
