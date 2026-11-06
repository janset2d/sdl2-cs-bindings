using Build.Data.Manifest.Models;
using Cake.Core.IO;

namespace Build.Data.Manifest;
/// <summary>
/// File-backed repository for <c>vcpkg.json</c> manifests. Path is supplied per call —
/// the repository is state-less so the same instance services every caller and every
/// manifest file across the build host. Missing-file / invalid-JSON / null-payload all
/// surface as <see cref="Cake.Core.CakeException"/>; iterating callers pre-check
/// existence with <see cref="Cake.Common.IO.FileAliases.FileExists"/> when "no file" and
/// "invalid file" need to be distinguished.
/// </summary>
public interface IVcpkgManifestRepository
{
    public VcpkgManifest Load(FilePath vcpkgManifestPath);
}
