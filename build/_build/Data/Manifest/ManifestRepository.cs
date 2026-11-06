using Build.Data.Manifest.Models;
using Build.Host.Cake;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Data.Manifest;

public interface IManifestRepository
{
    public ManifestConfig Load();
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
