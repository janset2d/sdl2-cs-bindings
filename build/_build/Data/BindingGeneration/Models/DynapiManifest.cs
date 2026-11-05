namespace Build.Data.BindingGeneration.Models;

/// <summary>
/// Parsed SDL2 dynapi manifest — the textual public-API oracle SDL ships at
/// <c>src/dynapi/SDL2.exports</c> in its source tree. SDL2's dynapi design
/// guarantees Linux libSDL2-2.0.so / macOS libSDL2.dylib / Windows SDL2.dll
/// all export the same symbol set, so a single text file is cross-platform
/// ground truth. The Janset binding generator reads it from vcpkg's buildtree
/// (see <see cref="DynapiManifestRepository"/> for the cache-state reach
/// mechanisms — local image-bake, CI multi-path cache, host dev machine).
/// </summary>
public sealed record DynapiManifest(IReadOnlySet<string> PublicSymbols, DynapiManifestOrigin Origin, string SourcePath);

/// <summary>
/// How the manifest was resolved at task time. Used in diagnostic messages so
/// failures point at the right path.
/// </summary>
public enum DynapiManifestOrigin
{
    /// <summary>
    /// Read from <c>external/vcpkg/buildtrees/sdl2/src/&lt;sha&gt;.clean/src/dynapi/SDL2.exports</c>
    /// (vcpkg's extracted SDL2 source tree). See <see cref="DynapiManifestRepository"/>
    /// for the reach mechanisms that keep this path populated across cache-hit runs.
    /// </summary>
    VcpkgBuildtree,
}
