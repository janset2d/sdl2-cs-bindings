namespace Build.Features.Harvesting;

public interface IArtifactDeployer
{
    Task<CopierResult> DeployArtifactsAsync(DeploymentPlan plan, CancellationToken ct = default);
}
