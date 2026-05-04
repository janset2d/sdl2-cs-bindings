using System.CommandLine;

namespace Build.Host.Cli.Options;

/// <summary>
/// CLI options consumed only by <c>--target ResolveVersionsFromManifest</c>.
/// Sister file: <c>ResolveVersionsFromExplicitOptions.cs</c> (operator-supplied versions).
/// Stage targets (PreFlight / Package / ConsumerSmoke / PublishStaging) read
/// <c>StageVersionsOptions.VersionsFileOption</c> instead.
/// </summary>
public static class ResolveVersionsFromManifestOptions
{
    /// <summary>
    /// Prerelease suffix appended by manifest-derived version composition. Required by
    /// <c>ResolveVersionsFromManifestTask</c>; empty/whitespace fails loud at task entry.
    /// </summary>
    public static readonly Option<string?> VersionSuffixOption = new(
        aliases: ["--suffix"],
        description: "Prerelease suffix appended to <UpstreamMajor>.<UpstreamMinor>.0 by ResolveVersionsFromManifest, e.g. 'local.<timestamp>' or 'ci.<run-id>'. Required when invoking --target ResolveVersionsFromManifest.");

    /// <summary>
    /// Repeated family identifier filter. Empty list means all families in
    /// <c>manifest.package_families[]</c>. Non-empty subset filters to those families.
    /// Consumed only by <c>ResolveVersionsFromManifestTask</c> — the explicit task takes
    /// scope from its operator input mapping, not from this flag.
    /// </summary>
    public static readonly Option<List<string>> VersionScopeOption = new(
        aliases: ["--scope"],
        description: "Repeated family identifier filter for ResolveVersionsFromManifest output. Each occurrence is one family name (e.g. --scope sdl2-core --scope sdl2-image). Empty = all families. ResolveVersionsFromExplicit ignores this flag — the operator-supplied mapping IS the scope there.")
    {
        Arity = ArgumentArity.ZeroOrMore,
    };
}
