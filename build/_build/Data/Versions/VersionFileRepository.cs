using Build.Host.Cake;
using Build.Versioning;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Data.Versions;

public interface IVersionFileRepository
{
    PackageFamilyVersionSet Load(FilePath path);

    Task SaveAsync(FilePath path, PackageFamilyVersionSet versions);
}

public sealed class VersionFileRepository(ICakeContext context) : IVersionFileRepository
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public PackageFamilyVersionSet Load(FilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!_context.FileExists(path))
        {
            throw new CakeException(
                $"VersionFileRepository cannot load versions: file does not exist at '{path.FullPath}'. " +
                "Run --target ResolveVersionsFromManifest or ResolveVersionsFromExplicit first to produce the versions.json.");
        }

        return _context.ToJson<PackageFamilyVersionSet>(path);
    }

    public async Task SaveAsync(FilePath path, PackageFamilyVersionSet versions)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(versions);

        var directory = path.GetDirectory();
        if (!_context.DirectoryExists(directory))
        {
            _context.CreateDirectory(directory);
        }

        await _context.WriteJsonAsync(path, versions);
    }
}
