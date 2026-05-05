using Build.Shared.Manifest;

namespace Build.Repositories;

public interface IManifestRepository
{
    ManifestConfig Load();
}
