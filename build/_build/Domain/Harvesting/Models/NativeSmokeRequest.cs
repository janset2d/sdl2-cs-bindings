namespace Build.Domain.Harvesting.Models;

/// <summary>
/// ADR-003 §3.2 per-stage request driving <c>NativeSmokeTaskRunner</c>. NativeSmoke is the
/// per-RID native payload validation stage extracted from Harvest in Slice D — it proves
/// the harvested binaries load and initialize at the OS level without P/Invoke. Harvest
/// output root and CMake preset dir come from <c>IPathService</c>; the request surfaces only
/// the axis the caller controls (RID).
/// </summary>
/// <param name="Rid">Target RID whose <c>tests/smoke-tests/native-smoke/CMakePresets.json</c>
/// entry is driven (e.g., <c>win-x64</c>).</param>
public sealed record NativeSmokeRequest(string Rid);
