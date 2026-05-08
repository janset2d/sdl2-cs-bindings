using Build.Versioning;

namespace Build.Features.Packaging;

/// <summary>
/// Request for <c>PackagePipeline</c>. Carries the resolved per-family version set used for
/// every concrete pack invocation in this stage. Harvest output and package output directories
/// come from <c>IPathService</c>; the set's families define pack scope.
/// </summary>
/// <param name="Versions">Typed family→version set. Empty set is rejected by the runner —
/// pack always targets an explicit family selection.</param>
public sealed record PackRequest(PackageFamilyVersionSet Versions);
