using Build.Shared.Harvesting;
using Build.Shared.Manifest;
using Cake.Core.IO;

namespace Build.Features.Harvesting;

public interface IArtifactPlanner
{
    Task<ArtifactPlannerResult> CreatePlanAsync(LibraryManifest current, BinaryClosure closure, DirectoryPath outRoot, CancellationToken ct = default);
}
