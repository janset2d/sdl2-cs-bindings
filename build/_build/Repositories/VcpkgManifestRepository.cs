using Build.Vcpkg;
using Build.Shared.Manifest;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Repositories;

public sealed class VcpkgManifestRepository(ICakeContext context, IVcpkgManifestReader reader, FilePath vcpkgManifestPath)
    : IVcpkgManifestRepository
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly IVcpkgManifestReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    private readonly FilePath _vcpkgManifestPath = vcpkgManifestPath ?? throw new ArgumentNullException(nameof(vcpkgManifestPath));

    public VcpkgManifest Load()
    {
        if (!_context.FileExists(_vcpkgManifestPath))
        {
            throw new CakeException(
                $"VcpkgManifestRepository cannot load vcpkg manifest: file does not exist at '{_vcpkgManifestPath.FullPath}'. " +
                "Run from the repository root or pass --repo-root to point Cake at a valid checkout.");
        }

        return _reader.ParseFile(_vcpkgManifestPath);
    }
}
