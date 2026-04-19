using Build.Context.Models;
using Build.Domain.Preflight.Results;
using Cake.Core.IO;

namespace Build.Domain.Preflight;

public interface IVersionConsistencyValidator
{
    VersionConsistencyResult Validate(ManifestConfig manifest, VcpkgManifest vcpkgManifest, FilePath manifestPath, FilePath vcpkgManifestPath);
}
