using NuGet.Versioning;

namespace Build.Context.Configs;

/// <summary>
/// Stage-level version input: an operator-supplied mapping of family identifier to NuGet
/// version. The mapping is populated in <c>Program.cs</c> from repeated
/// <c>--explicit-version</c> CLI entries and then shared with stage runners.
/// </summary>
public sealed class PackageBuildConfiguration(IReadOnlyDictionary<string, NuGetVersion> explicitVersions)
{
    public IReadOnlyDictionary<string, NuGetVersion> ExplicitVersions { get; } =
        explicitVersions ?? throw new ArgumentNullException(nameof(explicitVersions));
}
