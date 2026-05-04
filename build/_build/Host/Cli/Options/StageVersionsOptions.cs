using System.CommandLine;

namespace Build.Host.Cli.Options;

/// <summary>
/// CLI options consumed by stage targets (PreFlight, Package, PackageConsumerSmoke,
/// PublishStaging). ResolveVersions-only options live in
/// <c>ResolveVersionsOptions.cs</c>.
/// </summary>
public static class StageVersionsOptions
{
    /// <summary>
    /// Path to a flat <c>{ "family": "semver", ... }</c> JSON file produced by
    /// <c>--target ResolveVersions</c> and consumed by stage targets.
    /// ResolveVersions does not consume this flag.
    /// </summary>
    public static readonly Option<string?> VersionsFileOption = new(
        aliases: ["--versions-file"],
        description: "Stage-target input. Path to flat {family: semver} JSON from ResolveVersions output. PreFlight/Package/ConsumerSmoke/PublishStaging consume this");
}
