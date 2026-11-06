
namespace Build.Data.BindingGeneration.Models;
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
