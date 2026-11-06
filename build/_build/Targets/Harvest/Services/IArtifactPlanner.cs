using Build.Data.Manifest.Models;
using Build.Results;
using Build.Targets.Harvest.Models;
using Cake.Core.IO;

namespace Build.Targets.Harvest.Services;
public interface IArtifactPlanner
{
    public Task<Result<DeploymentPlan, ArtifactPlannerError>> CreatePlanAsync(
        LibraryManifest current,
        BinaryClosure closure,
        DirectoryPath outRoot,
        string corePackageName,
        CancellationToken ct = default);
}
