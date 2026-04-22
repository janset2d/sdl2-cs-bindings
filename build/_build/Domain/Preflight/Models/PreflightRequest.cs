using NuGet.Versioning;

namespace Build.Domain.Preflight.Models;

/// <summary>
/// ADR-003 §3.2 per-stage request carrying the resolved per-family version mapping that
/// drives <c>PreflightTaskRunner</c>. PreFlight is version-aware by contract (ADR-003 §2.3):
/// every invocation receives the mapping, and version-aware guardrails (G54, G58 scope-contains
/// mirror) run alongside structural validators on every call. The mapping is immutable across
/// the invocation per the resolve-once invariant (ADR-003 §2.4) — downstream stages receive
/// byte-equal copies.
/// </summary>
/// <param name="Versions">Case-insensitive per-family mapping. Keys follow the canonical
/// <c>sdl&lt;major&gt;-&lt;role&gt;</c> form defined by <c>FamilyIdentifierConventions</c>.
/// Empty mapping is rejected by the runner with an actionable error.</param>
public sealed record PreflightRequest(IReadOnlyDictionary<string, NuGetVersion> Versions);
