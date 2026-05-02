using System.Text.Json;

namespace Build.Features.Harvesting;

/// <summary>
/// Single source of truth for the JSON serialization contract used by the harvest pipeline
/// (both <c>rid-status/{rid}.json</c> written by <c>HarvestTask</c> and
/// <c>harvest-manifest.json</c> / <c>harvest-summary.json</c> written by
/// <c>ConsolidateHarvestTask</c>).
/// <para>
/// Exposed publicly so test fixtures can produce byte-identical output via the same options —
/// per the project rule that fixtures load real JSON rather than duplicate static strings.
/// </para>
/// </summary>
public static class HarvestJsonContract
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
