using Build.Context.Models;
using Build.Domain.Harvesting.Models;
using Build.Domain.Harvesting.Results;
using Cake.Core.IO;

namespace Build.Application.Harvesting;

public interface IArtifactPlanner
{
    Task<ArtifactPlannerResult> CreatePlanAsync(LibraryManifest current, BinaryClosure closure, DirectoryPath outRoot, CancellationToken ct = default);
}
