using Build.Data.Manifest.Models;

namespace Build.Data.Manifest;
public interface IManifestRepository
{
    public ManifestConfig Load();
}
