using NuGet.Versioning;

namespace Build.Features.Preflight;

/// <summary>
/// Request for <c>PreflightTaskRunner</c>.
/// PreFlight is version-aware, so every invocation carries the resolved per-family version
/// mapping and runs version-aware guardrails alongside structural validators.
/// </summary>
/// <param name="Versions">Case-insensitive per-family mapping. Keys follow the canonical
/// <c>sdl&lt;major&gt;-&lt;role&gt;</c> form defined by <c>FamilyIdentifierConventions</c>.
/// Empty mapping is rejected by the runner with an actionable error.</param>
public sealed record PreflightRequest(IReadOnlyDictionary<string, NuGetVersion> Versions);
