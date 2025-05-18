using Build.Context.Models;
using Build.Modules.Harvesting.Models;
using Cake.Core.IO;

namespace Build.Modules.Contracts;

public interface IArtifactPlanner
{
    Task<ArtifactPlan> CreatePlanAsync(LibraryManifest current, BinaryClosure closure, DirectoryPath outRoot, CancellationToken ct = default);
}