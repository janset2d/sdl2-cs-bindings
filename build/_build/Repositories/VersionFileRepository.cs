using Build.Host.Cake;
using Build.Versioning;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Repositories;

public sealed class VersionFileRepository : IVersionFileRepository
{
    private readonly ICakeContext _context;
    private readonly FilePath _versionsPath;

    public VersionFileRepository(ICakeContext context, FilePath versionsPath)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _versionsPath = versionsPath ?? throw new ArgumentNullException(nameof(versionsPath));
    }

    public PackageFamilyVersionSet Load()
    {
        return _context.ToJson<PackageFamilyVersionSet>(_versionsPath);
    }

    public async Task SaveAsync(PackageFamilyVersionSet versions)
    {
        ArgumentNullException.ThrowIfNull(versions);
        await _context.WriteJsonAsync(_versionsPath, versions);
    }
}
