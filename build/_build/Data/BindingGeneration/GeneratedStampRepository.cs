using Build.Data.BindingGeneration.Models;
using Build.Host.Cake;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Data.BindingGeneration;

public interface IGeneratedStampRepository
{
    Task<GeneratedStamp> LoadAsync(DirectoryPath outputDirectory, CancellationToken ct = default);

    Task SaveAsync(DirectoryPath outputDirectory, GeneratedStamp stamp, CancellationToken ct = default);
}

public sealed class GeneratedStampRepository(ICakeContext context) : IGeneratedStampRepository
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<GeneratedStamp> LoadAsync(DirectoryPath outputDirectory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ct.ThrowIfCancellationRequested();

        var path = GetStampPath(outputDirectory);
        var json = await _context.ReadAllTextAsync(path).ConfigureAwait(false);
        return CakeJsonExtensions.DeserializeJson<GeneratedStamp>(json)
            ?? throw new CakeException($"Failed to deserialize generated stamp from '{path.FullPath}'. JSON content might be 'null'.");
    }

    public async Task SaveAsync(DirectoryPath outputDirectory, GeneratedStamp stamp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(stamp);
        ct.ThrowIfCancellationRequested();

        var path = GetStampPath(outputDirectory);
        var json = _context.SerializeJson(stamp);
        await _context.WriteAllTextAsync(path, json).ConfigureAwait(false);
    }

    private static FilePath GetStampPath(DirectoryPath outputDirectory)
    {
        return outputDirectory.CombineWithFilePath(".generated-stamp");
    }
}
