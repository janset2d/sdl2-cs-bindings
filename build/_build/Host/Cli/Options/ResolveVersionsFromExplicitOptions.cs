using System.CommandLine;

namespace Build.Host.Cli.Options;

/// <summary>
/// CLI options consumed only by <c>--target ResolveVersionsFromExplicit</c>.
/// Sister file: <c>ResolveVersionsFromManifestOptions.cs</c> (manifest-derived versions).
/// Stage targets (PreFlight / Package / ConsumerSmoke / PublishStaging) read
/// <c>StageVersionsOptions.VersionsFileOption</c> instead.
/// <para>
/// <see cref="ExplicitVersionOption"/> and <see cref="ExplicitVersionsOption"/> are
/// mutually exclusive at task entry — supply one shape or the other, never both. The
/// task itself enforces the mutex (Program.cs stays thin: parse + DI wiring only).
/// </para>
/// </summary>
public static class ResolveVersionsFromExplicitOptions
{
    /// <summary>
    /// Repeated <c>--explicit-version family=semver</c> entries. Operator-supplied mapping
    /// for <c>ResolveVersionsFromExplicit</c>. Mutually exclusive with
    /// <see cref="ExplicitVersionsOption"/>.
    /// </summary>
    public static readonly Option<List<string>> ExplicitVersionOption = new(
        aliases: ["--explicit-version"],
        description: "Operator-supplied family-version mapping, repeated per family. Each is 'family=semver' (e.g., --explicit-version sdl2-core=2.32.0 --explicit-version sdl2-image=2.8.0). Consumed only by --target ResolveVersionsFromExplicit. Mutually exclusive with --explicit-versions. Stage targets read --versions-file.")
    {
        Arity = ArgumentArity.ZeroOrMore,
    };

    /// <summary>
    /// Comma-separated <c>--explicit-versions family=semver,...</c> single string. Exists
    /// to eliminate bash-level parsing in release.yml — the workflow passes
    /// <c>inputs.explicit-versions</c> directly. Mutually exclusive with
    /// <see cref="ExplicitVersionOption"/>.
    /// </summary>
    public static readonly Option<string?> ExplicitVersionsOption = new(
        aliases: ["--explicit-versions"],
        description: "Comma-separated family-version mapping, single string (e.g. 'sdl2-core=2.32.0,sdl2-image=2.8.0'). Consumed only by --target ResolveVersionsFromExplicit. Mutually exclusive with --explicit-version. Stage targets read --versions-file.");
}
