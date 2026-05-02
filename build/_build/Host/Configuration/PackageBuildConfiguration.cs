using NuGet.Versioning;

namespace Build.Host.Configuration;

/// <summary>
/// Operator-supplied version input: a mapping of family identifier to NuGet version. The
/// mapping is populated in <c>Program.cs</c> from repeated <c>--explicit-version</c> CLI
/// entries or <c>--versions-file</c>, then shared with stage runners and
/// <c>ResolveVersions --version-source=explicit</c>.
/// </summary>
public sealed class PackageBuildConfiguration(IReadOnlyDictionary<string, NuGetVersion> explicitVersions)
{
    public IReadOnlyDictionary<string, NuGetVersion> ExplicitVersions { get; } =
        explicitVersions ?? throw new ArgumentNullException(nameof(explicitVersions));
}
