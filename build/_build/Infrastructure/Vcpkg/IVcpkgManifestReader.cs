using Build.Context.Models;
using Cake.Core.IO;

namespace Build.Infrastructure.Vcpkg;

public interface IVcpkgManifestReader
{
    VcpkgManifest Parse(string jsonContent);

    VcpkgManifest ParseFile(FilePath path);
}