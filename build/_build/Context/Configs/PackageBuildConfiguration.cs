using NuGet.Versioning;

namespace Build.Context.Configs;

/// <summary>
/// Post-ADR-003 stage-level version input: operator-supplied mapping of family identifier
/// to NuGet SemVer. Replaces the pre-B1 scalar <c>(Families, FamilyVersion)</c> pair. Scope
/// is implicit in the mapping's key set per ADR-003 §2.2 ("scope = versions.keys") — separate
/// <c>--family</c> input is retired. Populated in <c>Program.cs</c> from repeated
/// <c>--explicit-version</c> CLI entries; consumed by <c>PackageTaskRunner</c>,
/// <c>PackageConsumerSmokeRunner</c>, <c>PreflightTaskRunner</c>, and the
/// <c>IPackageVersionProvider</c> DI factory.
/// </summary>
public sealed class PackageBuildConfiguration(IReadOnlyDictionary<string, NuGetVersion> explicitVersions)
{
    public IReadOnlyDictionary<string, NuGetVersion> ExplicitVersions { get; } =
        explicitVersions ?? throw new ArgumentNullException(nameof(explicitVersions));
}
