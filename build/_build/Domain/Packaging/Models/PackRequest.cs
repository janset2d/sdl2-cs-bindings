using NuGet.Versioning;

namespace Build.Domain.Packaging.Models;

/// <summary>
/// ADR-003 §3.2 per-stage request driving <c>PackageTaskRunner</c>. Carries the resolved
/// per-family version mapping used for every concrete pack invocation in this stage
/// (one managed <c>.nupkg</c> + one native <c>.nupkg</c> per family at the supplied
/// version). Harvest output + package output directories come from <c>IPathService</c>;
/// the scope of the operation IS the mapping's key set (ADR-003 §2.2 "scope = versions.keys").
/// </summary>
/// <param name="Versions">Case-insensitive per-family mapping. Empty mapping is rejected by
/// the runner — pack always targets an explicit family set.</param>
public sealed record PackRequest(IReadOnlyDictionary<string, NuGetVersion> Versions);
