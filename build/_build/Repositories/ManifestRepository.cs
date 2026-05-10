using Build.Host.Cake;
using Build.Manifest;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Repositories;

public interface IManifestRepository
{
    ManifestConfig Load();
}

public sealed class ManifestRepository(ICakeContext context, FilePath manifestPath) : IManifestRepository
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly FilePath _manifestPath = manifestPath ?? throw new ArgumentNullException(nameof(manifestPath));

    public ManifestConfig Load()
    {
        return _context.ToJson<ManifestConfig>(_manifestPath);
    }
}
