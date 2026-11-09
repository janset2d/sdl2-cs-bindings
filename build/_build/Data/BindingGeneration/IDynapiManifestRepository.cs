using Build.Data.BindingGeneration.Models;
using Build.Host.Paths;
using Build.Results;

namespace Build.Data.BindingGeneration;

/// <summary>
/// Loads SDL2's dynapi manifest (<c>SDL2.exports</c>) from vcpkg's buildtree.
/// <para>
/// vcpkg's binary cache stores compiled artifacts only — <c>installed/&lt;triplet&gt;/&lt;port&gt;/</c>
/// contents. On a binary-cache hit vcpkg unpacks the archive directly and skips source
/// extraction, so <c>buildtrees/sdl2/src/</c> is empty on the run that consumes the cache.
/// The manifest source must be preserved separately from the binary cache:
/// </para>
/// <list type="bullet">
///   <item><description><b>Local container (binding-generator image):</b> <c>vcpkg install</c>
///   runs at image build time in a dedicated Docker layer; buildtrees lives inside the image.
///   Docker layer cache invalidates on vcpkg.json / overlay / submodule changes.</description></item>
///   <item><description><b>Host dev machine:</b> <c>tools.cs setup</c> (or any
///   <c>vcpkg install sdl2</c> flow) populates buildtrees once; subsequent invocations
///   preserve it.</description></item>
/// </list>
/// The glob path the repository scans is composed by
/// <see cref="IPathService.GetSdl2DynapiExportsGlob"/>; this class holds no hardcoded paths.
/// </summary>
public interface IDynapiManifestRepository
{
    public Task<Result<DynapiManifest, ManifestResolutionError>> LoadAsync(CancellationToken ct = default);
}
