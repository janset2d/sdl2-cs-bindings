using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;

namespace Build.Modules.Contracts;

public interface IArtifactDeployer
{
    Task<CopierResult> DeployArtifactsAsync(DeploymentPlan plan, CancellationToken ct = default);
}
