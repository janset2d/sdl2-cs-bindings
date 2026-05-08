using Build.Shared.Manifest;

namespace Build.Repositories;

/// <summary>
/// Practical file adapter for the repository's vcpkg manifest. Mirrors
/// <see cref="IManifestRepository"/>: knows the canonical file path via DI,
/// parses the manifest via <see cref="Build.Integrations.Vcpkg.IVcpkgManifestReader"/>,
/// and surfaces missing-file failures as <see cref="Cake.Core.CakeException"/>
/// with an operator-friendly message before the reader is invoked.
/// </summary>
public interface IVcpkgManifestRepository
{
    VcpkgManifest Load();
}
