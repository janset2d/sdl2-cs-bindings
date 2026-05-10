using Build.Harvesting;
using Build.Manifest;
using Build.Results;
using Build.Targets.Harvest.Models;
using Cake.Core.IO;

namespace Build.Targets.Harvest.Services;

public interface IArtifactPlanner
{
    Task<Result<DeploymentPlan, ArtifactPlannerError>> CreatePlanAsync(LibraryManifest current, BinaryClosure closure, DirectoryPath outRoot, CancellationToken ct = default);
}
