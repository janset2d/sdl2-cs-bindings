using Build.Domain.Harvesting.Models;
using Build.Domain.Harvesting.Results;

namespace Build.Application.Harvesting;

public interface IArtifactDeployer
{
    Task<CopierResult> DeployArtifactsAsync(DeploymentPlan plan, CancellationToken ct = default);
}
