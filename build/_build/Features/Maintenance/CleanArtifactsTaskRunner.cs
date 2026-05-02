using Build.Host.Paths;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Features.Maintenance;

/// <summary>
/// Wipes the ephemeral build-output subtrees so a fresh run starts from a clean slate.
/// Does not touch <c>vcpkg_installed/</c> because rebuilding that directory is expensive and
/// it is safe to reuse across invocations.
/// </summary>
public sealed class CleanArtifactsTaskRunner(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    public Task RunAsync()
    {
        foreach (var directory in GetDirectoriesToClean())
        {
            if (_cakeContext.DirectoryExists(directory))
            {
                _log.Information("Cleaning '{0}'.", directory.FullPath);
                _cakeContext.DeleteDirectory(directory, new DeleteDirectorySettings { Recursive = true, Force = true });
            }
            else
            {
                _log.Debug("Skipping '{0}' — does not exist.", directory.FullPath);
            }
        }

        _log.Information("CleanArtifacts completed.");
        return Task.CompletedTask;
    }

    private IEnumerable<DirectoryPath> GetDirectoriesToClean()
    {
        yield return _pathService.HarvestOutput;
        yield return _pathService.PackagesOutput;
        yield return _pathService.PackageConsumerSmokeOutput;
        yield return _pathService.SmokeTestResultsOutput;
        yield return _pathService.HarvestStagingRoot;
        yield return _pathService.InspectOutputRoot;
        yield return _pathService.MatrixOutputRoot;
        yield return _pathService.NativeSmokeBuildRoot;
        yield return _pathService.ResolveVersionsOutputDirectory;
    }
}
