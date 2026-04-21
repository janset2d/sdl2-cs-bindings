using System.Collections.Immutable;
using Build.Context;
using Build.Context.Models;
using Build.Domain.Ci.Models;
using Build.Domain.Paths;
using Cake.Core;
using Cake.Core.Diagnostics;

namespace Build.Application.Ci;

/// <summary>
/// Emits the GitHub-Actions-shape matrix JSON derived from <see cref="ManifestConfig.Runtimes"/>.
/// Single output file (<c>artifacts/matrix/runtimes.json</c>) consumed by both the <c>harvest</c>
/// and <c>native-smoke</c> jobs in <c>release.yml</c>. The native-smoke job shares the harvest
/// matrix shape — the four PA-2 RIDs naturally fail at <c>cmake --preset &lt;rid&gt;</c> until the
/// CMake preset file grows in Phase 2b (no code-level cap in the runner).
/// </summary>
public sealed class GenerateMatrixTaskRunner(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    ManifestConfig manifestConfig)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));

    public async Task RunAsync()
    {
        var entries = _manifestConfig.Runtimes
            .Select(r => new MatrixEntry
            {
                Rid = r.Rid,
                Triplet = r.Triplet,
                Strategy = r.Strategy,
                Runner = r.Runner,
                ContainerImage = r.ContainerImage,
            })
            .ToImmutableList();

        if (entries.Count == 0)
        {
            throw new CakeException("GenerateMatrix: manifest.runtimes[] is empty — cannot emit a CI matrix.");
        }

        var output = new MatrixOutput { Include = entries };
        var outputFile = _pathService.GetMatrixOutputFile();

        _log.Information("GenerateMatrix: writing {0} RID entries to '{1}'.", entries.Count, outputFile.FullPath);
        await _cakeContext.WriteJsonAsync(outputFile, output);
    }
}
