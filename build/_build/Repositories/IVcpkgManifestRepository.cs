using Build.Manifest;

namespace Build.Repositories;

/// <summary>
/// File-backed repository for the repository-root <c>vcpkg.json</c> manifest. Loads the
/// canonical file via <see cref="Cake.Core.ICakeContext.FileSystem"/>, deserializes through
/// the project's central JSON surface, and surfaces missing-file or invalid-JSON failures as
/// <see cref="Cake.Core.CakeException"/> with operator-friendly messages.
/// </summary>
public interface IVcpkgManifestRepository
{
    VcpkgManifest Load();
}
