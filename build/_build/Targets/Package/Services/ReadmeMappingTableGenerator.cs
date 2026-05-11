using Build.Host.Cake;
using Build.Host.Paths;
using Build.Data.Manifest.Models;
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
    Task UpdateAsync(ManifestConfig manifestConfig, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ReadmeMappingTableGenerator(IPathService pathService, ICakeContext cakeContext) : IReadmeMappingTableGenerator
{
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));

    public async Task UpdateAsync(ManifestConfig manifestConfig, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifestConfig);
        var readmePath = _pathService.GetReadmeFile();

        if (!_cakeContext.FileExists(readmePath))
        {
            throw new InvalidOperationException($"README mapping table generation failed: file '{readmePath.FullPath}' does not exist.");
        }

        var expectedBlock = ReadmeMappingTableBlock.BuildBlock(manifestConfig);
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
