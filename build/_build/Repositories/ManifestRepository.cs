using Build.Host.Cake;
using Build.Shared.Manifest;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Repositories;

public sealed class ManifestRepository : IManifestRepository
{
    private readonly ICakeContext _context;
    private readonly FilePath _manifestPath;

    public ManifestRepository(ICakeContext context, FilePath manifestPath)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _manifestPath = manifestPath ?? throw new ArgumentNullException(nameof(manifestPath));
    }

    public ManifestConfig Load()
    {
        return _context.ToJson<ManifestConfig>(_manifestPath);
    }
}
