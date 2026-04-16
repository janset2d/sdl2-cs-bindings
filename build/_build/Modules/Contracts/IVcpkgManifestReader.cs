using Build.Context.Models;
using Cake.Core.IO;

namespace Build.Modules.Contracts;

public interface IVcpkgManifestReader
{
    VcpkgManifest Parse(string jsonContent);

    VcpkgManifest ParseFile(FilePath path);
}