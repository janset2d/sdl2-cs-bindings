using Build.Modules.Preflight.Models;
using Build.Context.Models;
using Cake.Core.IO;

namespace Build.Modules.Contracts;

public interface IVersionConsistencyValidator
{
    VersionConsistencyValidation Validate(ManifestConfig manifest, VcpkgManifest vcpkgManifest, FilePath manifestPath, FilePath vcpkgManifestPath);
}
