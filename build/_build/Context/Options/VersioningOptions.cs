using System.CommandLine;

namespace Build.Context.Options;

/// <summary>
/// Version-resolution CLI options.
/// <list type="bullet">
///   <item><see cref="VersionSourceOption"/> (<c>--version-source</c>) selects the provider
///     path for the <c>ResolveVersions</c> target (<c>manifest</c>, <c>explicit</c>,
///     <c>git-tag</c>, <c>meta-tag</c>).</item>
///   <item><see cref="VersionSuffixOption"/> (<c>--suffix</c>) supplies the prerelease suffix
///     appended by <c>ManifestVersionProvider</c>.</item>
///   <item><see cref="VersionScopeOption"/> (<c>--scope</c>) filters families for
///     <c>ResolveVersions</c>; empty means all families.</item>
///   <item><see cref="ExplicitVersionOption"/> (<c>--explicit-version</c>) carries repeated
///     <c>family=semver</c> entries for stage-target execution and for
///     <c>ResolveVersions --version-source=explicit</c>.</item>
/// </list>
/// </summary>
public static class VersioningOptions
{
    public static readonly Option<string?> VersionSourceOption = new(
        aliases: ["--version-source"],
        description: "Version source for the ResolveVersions target (manifest | explicit | git-tag | meta-tag). Required for ResolveVersions.");

    public static readonly Option<string?> VersionSuffixOption = new(
        aliases: ["--suffix"],
        description: "Prerelease suffix appended by ManifestVersionProvider, e.g., 'local.<timestamp>' or 'ci.<run-id>'. Required when --version-source=manifest.");

    public static readonly Option<List<string>> VersionScopeOption = new(
        aliases: ["--scope"],
        description: "Repeated family identifier filter for ResolveVersions. Each occurrence is one family name (e.g., --scope sdl2-core --scope sdl2-image). Empty = all families.")
    {
        Arity = ArgumentArity.ZeroOrMore,
    };

    public static readonly Option<List<string>> ExplicitVersionOption = new(
        aliases: ["--explicit-version"],
        description: "Operator-supplied family-version mapping, repeated per family. Each occurrence is 'family=semver' (e.g., --explicit-version sdl2-core=2.32.0 --explicit-version sdl2-image=2.8.0). Used by stage targets and ResolveVersions --version-source=explicit.")
    {
        Arity = ArgumentArity.ZeroOrMore,
    };

    /// <summary>
    /// Path to a JSON file containing a flat <c>{ "family": "semver", ... }</c> mapping.
    /// Entries are merged with <see cref="ExplicitVersionOption"/> CLI entries; overlap is
    /// rejected as ambiguous. Typical CI usage:
    /// <c>--versions-file artifacts/resolve-versions/versions.json</c>.
    /// </summary>
    public static readonly Option<string?> VersionsFileOption = new(
        aliases: ["--versions-file"],
        description: "Path to a JSON file with a flat { family: semver } mapping. Merged with --explicit-version entries; overlap is rejected.");
}
