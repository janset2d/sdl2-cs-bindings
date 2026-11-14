using System.CommandLine;

namespace Build.Host.Cli.Options;

/// <summary>
/// CLI options for <c>--target ResolveVersionsFromManifest</c>; GenerateBindings also consumes --scope.
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
    public static readonly Option<string?> VersionSuffixOption = new("--suffix")
    {
        Description = "Prerelease suffix appended to <UpstreamMajor>.<UpstreamMinor>.0 by ResolveVersionsFromManifest, e.g. 'local.<timestamp>' or " +
            "'ci.<run-id>'. Required when invoking --target ResolveVersionsFromManifest.",
    };

    /// <summary>
    /// Repeated family identifier filter. For <c>ResolveVersionsFromManifest</c>, an empty list
    /// means all package families in <c>manifest.package_families[]</c>; a non-empty list filters to those families.
    /// For <c>GenerateBindings</c>, an empty list means every enabled family in generation order;
    /// an unknown or disabled identifier is an error. The explicit version task takes scope
    /// from its operator input mapping, not from this flag.
    /// </summary>
    public static readonly Option<List<string>> VersionScopeOption = new("--scope")
    {
        Description = "Repeated family identifier filter for ResolveVersionsFromManifest or GenerateBindings planning, generation and evidence. " +
            "Each occurrence is one family name (e.g. --scope " +
            "sdl2-core --scope sdl2-image). ResolveVersionsFromManifest: empty = all package families; non-empty = filter to those families. " +
            "GenerateBindings: empty = every enabled family in generation order; an unknown or disabled id is an error. " +
            "ResolveVersionsFromExplicit ignores this flag — the operator-supplied mapping IS the scope there.",
        Arity = ArgumentArity.ZeroOrMore,
    };
}
