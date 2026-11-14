namespace Build.Data.BindingGeneration.Models;

/// <summary>
/// Failure modes when resolving the dynapi manifest. Each carries an actionable
/// diagnostic message; callers should surface it to the operator without
/// paraphrasing.
/// </summary>
public sealed record ManifestResolutionError(ManifestResolutionErrorKind Kind, string Reason)
{
    public static ManifestResolutionError NotFound(string globPath, string installedGlobPath) =>
        new(
            ManifestResolutionErrorKind.NotFound,
            $"SDL2 dynapi manifest not found under '{globPath}' or '{installedGlobPath}'. The binding-generator container bakes vcpkg state "
            + "at image build time; CI's vcpkg-setup action caches buildtrees/sdl2/src alongside the binary cache. "
            + "On a host dev machine the path is populated by any vcpkg install sdl2 flow (tools.cs setup "
            + "or direct vcpkg invocation).");

    public static ManifestResolutionError AmbiguousGlobMatches(string globPath, string installedGlobPath, string foundPaths) =>
        new(
            ManifestResolutionErrorKind.Ambiguous,
            $"SDL2 dynapi manifest globs '{globPath}' and '{installedGlobPath}' matched multiple candidates: {foundPaths}. "
            + "This usually means a stale buildtree from a previous vcpkg pin was left on disk after a "
            + "version downgrade. Delete the stale tree under whichever search root holds it before re-running.");

    public static ManifestResolutionError ParseFailure(string sourcePath, string detail) =>
        new(ManifestResolutionErrorKind.ParseFailure, $"SDL2 dynapi manifest at '{sourcePath}' failed to parse: {detail}");
}
