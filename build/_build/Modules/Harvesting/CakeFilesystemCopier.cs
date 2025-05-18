using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;

namespace Build.Modules.Harvesting;

public sealed class CakeFilesystemCopier(ICakeContext ctx) : IFilesystemCopier
{
    private readonly ICakeContext _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    private readonly ICakeLog _log = ctx.Log;

    public async Task CopyAsync(IEnumerable<NativeArtifact> artifacts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(artifacts);

        foreach (var art in artifacts)
        {
            ct.ThrowIfCancellationRequested();

            _ctx.EnsureDirectoryExists(art.TargetPath.GetDirectory());
            _ctx.CopyFile(art.SourcePath, art.TargetPath);
            _log.Verbose("copied {0} → {1}", art.SourcePath.GetFilename(), art.TargetPath);
            await Task.Yield();
        }
    }
}
