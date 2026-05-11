using System.Collections.Immutable;
using Build.Host;
using Build.Host.Cake;
using Build.Host.Paths;
using Build.Data.Manifest.Models;
using Build.Targets.GenerateMatrix.Models;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Targets.GenerateMatrix;

[TaskName("GenerateMatrix")]
[TaskDescription("Emits artifacts/matrix/runtimes.json — the GitHub-Actions matrix derived from manifest.runtimes[]")]
public sealed class GenerateMatrixTask(ICakeContext cakeContext, ICakeLog log, IPathService pathService, ManifestConfig manifestConfig) : AsyncFrostingTask<BuildContext>
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var entries = _manifestConfig.Runtimes
            .Select(r => new MatrixEntry
            {
                Rid = r.Rid,
                Triplet = r.Triplet,
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
