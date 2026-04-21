using System.CommandLine;

namespace Build.Context.Options;

/// <summary>
/// ADR-003 version-axis CLI options. Four options land in Slice B1:
/// <list type="bullet">
///   <item><see cref="VersionSourceOption"/> (<c>--version-source</c>) — selects the provider
///     path for the <c>ResolveVersions</c> target (<c>manifest</c>, <c>explicit</c>,
///     <c>git-tag</c>, <c>meta-tag</c>). Stage tasks never accept <c>--version-source</c>;
///     they consume <see cref="ExplicitVersionOption"/> directly.</item>
///   <item><see cref="VersionSuffixOption"/> (<c>--suffix</c>) — prerelease suffix appended by
///     <c>ManifestVersionProvider</c> (<c>--version-source=manifest</c>), e.g.
///     <c>local.&lt;timestamp&gt;</c> or <c>ci.&lt;run-id&gt;</c>.</item>
///   <item><see cref="VersionScopeOption"/> (<c>--scope</c>) — repeated family filter for
///     <c>ResolveVersions</c>; empty = all families.</item>
///   <item><see cref="ExplicitVersionOption"/> (<c>--explicit-version</c>) — repeated
///     <c>family=semver</c> mapping entries. Stage tasks' sole version input. Also consumed by
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
        description: "Operator-supplied family-version mapping, repeated per family. Each occurrence is 'family=semver' (e.g., --explicit-version sdl2-core=2.32.0 --explicit-version sdl2-image=2.8.0).")
    {
        Arity = ArgumentArity.ZeroOrMore,
    };
}
