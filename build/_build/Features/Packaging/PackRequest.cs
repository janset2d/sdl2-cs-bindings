using NuGet.Versioning;

namespace Build.Features.Packaging;

/// <summary>
/// Request for <c>PackageTaskRunner</c>.
/// Carries the resolved per-family version mapping used for every concrete pack invocation
/// in this stage. Harvest output and package output directories come from <c>IPathService</c>;
/// the mapping's key set defines pack scope.
/// </summary>
/// <param name="Versions">Case-insensitive per-family mapping. Empty mapping is rejected by
/// the runner — pack always targets an explicit family set.</param>
public sealed record PackRequest(IReadOnlyDictionary<string, NuGetVersion> Versions);
