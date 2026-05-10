using Build.Host.Cake;
using Build.Host.Paths;
using Build.Data.Manifest;
using Build.Targets.Package.Models;
using Cake.Common.IO;
using Cake.Core;

namespace Build.Targets.Package.Services;

/// <summary>
/// Generator (G57) — upserts the README mapping table block during packing. Runs once per
/// packaging invocation (not per family) because the block reflects the manifest state, not
/// a single family's state.
/// </summary>
public interface IReadmeMappingTableGenerator
{
    Task UpdateAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ReadmeMappingTableGenerator(ManifestConfig manifestConfig, IPathService pathService, ICakeContext cakeContext) : IReadmeMappingTableGenerator
{
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));

    public async Task UpdateAsync(CancellationToken ct = default)
    {
        var readmePath = _pathService.GetReadmeFile();

        if (!_cakeContext.FileExists(readmePath))
        {
            throw new InvalidOperationException($"README mapping table generation failed: file '{readmePath.FullPath}' does not exist.");
        }

        var expectedBlock = ReadmeMappingTableBlock.BuildBlock(_manifestConfig);
        var original = await _cakeContext.ReadAllTextAsync(readmePath);
        ct.ThrowIfCancellationRequested();

        var updated = ReadmeMappingTableBlock.UpsertBlock(original, expectedBlock);

        if (string.Equals(original, updated, StringComparison.Ordinal))
        {
            return;
        }

        await _cakeContext.WriteAllTextAsync(readmePath, updated);
        ct.ThrowIfCancellationRequested();
    }
}
