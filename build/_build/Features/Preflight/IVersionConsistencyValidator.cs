using Build.Shared.Manifest;
using Cake.Core.IO;

namespace Build.Features.Preflight;

public interface IVersionConsistencyValidator
{
    VersionConsistencyResult Validate(ManifestConfig manifest, VcpkgManifest vcpkgManifest, FilePath manifestPath, FilePath vcpkgManifestPath);
}
