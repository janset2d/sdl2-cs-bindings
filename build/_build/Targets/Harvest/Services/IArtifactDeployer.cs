using Build.Results;
using Build.Targets.Harvest.Models;

namespace Build.Targets.Harvest.Services;

public interface IArtifactDeployer
{
    public Task<Result<DeploymentStatistics, CopierError>> DeployArtifactsAsync(DeploymentPlan plan, CancellationToken ct = default);
}
