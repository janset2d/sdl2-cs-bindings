using Build.Manifest;

namespace Build.Repositories;

public interface IManifestRepository
{
    ManifestConfig Load();
}
