using NuGet.Versioning;

namespace Build.Host.Configuration;

/// <summary>
/// Resolved family→version mapping consumed by stage targets (PreFlight, Package,
/// PackageConsumerSmoke, PublishStaging). Populated only from <c>--versions-file</c>.
/// <para>
/// <c>--explicit-version</c> / <c>--explicit-versions</c> are ResolveVersions inputs
/// and live on <see cref="VersioningConfiguration"/>. Stage targets never see them
/// directly — operator input flows through ResolveVersions → versions.json → this record.
/// </para>
/// </summary>
public sealed class PackageBuildConfiguration(IReadOnlyDictionary<string, NuGetVersion> familyVersionMapping)
{
    public IReadOnlyDictionary<string, NuGetVersion> FamilyVersionMapping { get; } =
        familyVersionMapping ?? throw new ArgumentNullException(nameof(familyVersionMapping));
}
