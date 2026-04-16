using Build.Modules.Preflight.Models;
using Build.Modules.Preflight.Results;
using Build.Context.Models;
using Cake.Core.IO;

namespace Build.Modules.Contracts;

public interface IVersionConsistencyValidator
{
    VersionConsistencyResult Validate(ManifestConfig manifest, VcpkgManifest vcpkgManifest, FilePath manifestPath, FilePath vcpkgManifestPath);
}
