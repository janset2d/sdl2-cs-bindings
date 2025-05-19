using Build.Context.Models;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Cake.Core.IO;

namespace Build.Modules.Contracts;

public interface IArtifactPlanner
{
    Task<ArtifactPlannerResult> CreatePlanAsync(LibraryManifest current, BinaryClosure closure, DirectoryPath outRoot, CancellationToken ct = default);
}
