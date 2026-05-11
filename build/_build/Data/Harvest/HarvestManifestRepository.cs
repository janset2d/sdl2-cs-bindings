using System.Text.Json;
using Build.Host.Cake;
using Build.Host.Paths;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Data.Harvest;

/// <summary>
/// File-backed repository for the consolidated harvest JSON contracts produced by
/// ConsolidateHarvest and consumed by Package. Owns rid-status reads plus staged manifest /
/// summary writes; target services own aggregation and license-union policy.
/// </summary>
public interface IHarvestManifestRepository
{
    Task<IReadOnlyList<RidHarvestStatus>?> LoadRidStatusesAsync(string libraryName, CancellationToken ct = default);

    Task<HarvestManifest> LoadManifestAsync(string libraryName, CancellationToken ct = default);

    Task WriteManifestTempAsync(string libraryName, HarvestManifest manifest, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class HarvestManifestRepository(ICakeContext cakeContext, IPathService pathService) : IHarvestManifestRepository
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    /// <summary>
    /// Returns parsed RID status records for <paramref name="libraryName"/>, or
    /// <see langword="null"/> when the directory is missing, contains no JSON files, or all
    /// status files deserialize to <see langword="null"/>.
    /// </summary>
    public async Task<IReadOnlyList<RidHarvestStatus>?> LoadRidStatusesAsync(string libraryName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ct.ThrowIfCancellationRequested();

        var ridStatusDir = _pathService.GetHarvestLibraryRidStatusDir(libraryName);
        if (!_cakeContext.DirectoryExists(ridStatusDir))
        {
            return null;
        }

        var ridStatusFiles = _cakeContext.GetFiles($"{ridStatusDir}/*.json");
        if (ridStatusFiles.Count == 0)
        {
            return null;
        }

        var ridStatuses = new List<RidHarvestStatus>();
        var invalidStatusFiles = new List<string>();
        foreach (var statusFile in ridStatusFiles.OrderBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var jsonContent = await _cakeContext.ReadAllTextAsync(statusFile).ConfigureAwait(false);
                var ridStatus = CakeJsonExtensions.DeserializeJson<RidHarvestStatus>(jsonContent);
                if (ridStatus != null)
                {
                    ridStatuses.Add(ridStatus);
                }
            }
            catch (JsonException ex)
            {
                invalidStatusFiles.Add(FormatInvalidStatusFile(statusFile, ex.Message));
            }
            catch (IOException ex)
            {
                invalidStatusFiles.Add(FormatInvalidStatusFile(statusFile, ex.Message));
            }
            catch (CakeException ex)
            {
                invalidStatusFiles.Add(FormatInvalidStatusFile(statusFile, ex.Message));
            }
        }

        if (invalidStatusFiles.Count > 0)
        {
            throw new CakeException(
                $"RID status directory '{ridStatusDir.FullPath}' for library '{libraryName}' contains {invalidStatusFiles.Count} unreadable or invalid file(s): {string.Join("; ", invalidStatusFiles)}. " +
                "ConsolidateHarvest refuses to continue because silently dropping a RID status can shrink the consolidated license set and produce a false-green compliance surface.");
        }

        return ridStatuses.Count == 0 ? null : ridStatuses;
    }

    public async Task<HarvestManifest> LoadManifestAsync(string libraryName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ct.ThrowIfCancellationRequested();

        var manifestPath = _pathService.GetHarvestLibraryManifestFile(libraryName);
        var manifest = await _cakeContext.ToJsonAsync<HarvestManifest>(manifestPath).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return manifest;
    }

    public async Task WriteManifestTempAsync(string libraryName, HarvestManifest manifest, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentNullException.ThrowIfNull(manifest);
        ct.ThrowIfCancellationRequested();

        var manifestTempPath = _pathService.GetHarvestLibraryManifestTempFile(libraryName);
        await _cakeContext.WriteJsonAsync(manifestTempPath, manifest).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();
        var summaryTempPath = _pathService.GetHarvestLibrarySummaryTempFile(libraryName);
        await _cakeContext.WriteJsonAsync(summaryTempPath, manifest.Summary).ConfigureAwait(false);
    }

    private static string FormatInvalidStatusFile(FilePath statusFile, string message)
    {
        return $"{statusFile.GetFilename().FullPath}: {message}";
    }
}
