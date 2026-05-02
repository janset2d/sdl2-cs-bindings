using Build.Shared.Manifest;
using Cake.Core.IO;

namespace Build.Integrations.Vcpkg;

public interface IVcpkgManifestReader
{
    VcpkgManifest Parse(string jsonContent);

    VcpkgManifest ParseFile(FilePath path);
}
