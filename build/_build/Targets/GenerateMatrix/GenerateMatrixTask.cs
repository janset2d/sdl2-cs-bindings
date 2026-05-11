using System.Collections.Immutable;
using Build.Data.Manifest;
using Build.Host;
using Build.Host.Cake;
using Build.Host.Paths;
using Build.Targets.GenerateMatrix.Models;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Targets.GenerateMatrix;

[TaskName("GenerateMatrix")]
[TaskDescription("Emits artifacts/matrix/runtimes.json — the GitHub-Actions matrix derived from manifest.runtimes[]")]
public sealed class GenerateMatrixTask(ICakeContext cakeContext, ICakeLog log, IPathService pathService, IManifestRepository manifestRepository) : AsyncFrostingTask<BuildContext>
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly IManifestRepository _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var manifest = _manifestRepository.Load();
        var entries = manifest.Runtimes
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
