using System.Diagnostics.CodeAnalysis;
using Build.Modules.Harvesting.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Modules.Harvesting.Results;

public sealed class ArtifactPlannerResult(OneOf<Error<HarvestingError>, Success<DeploymentPlan>> result) : Result<HarvestingError, DeploymentPlan>(result)
{
    public static implicit operator ArtifactPlannerResult(HarvestingError error) => new(new Error<HarvestingError>(error));
    public static implicit operator ArtifactPlannerResult(DeploymentPlan artifactPlan) => new(new Success<DeploymentPlan>(artifactPlan));

    public static explicit operator HarvestingError(ArtifactPlannerResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT0.Value;
    }

    public static explicit operator DeploymentPlan(ArtifactPlannerResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT1.Value;
    }

    public static ArtifactPlannerResult FromHarvestingError(HarvestingError error) => error;
    public static ArtifactPlannerResult FromDeploymentPlan(DeploymentPlan artifactPlan) => artifactPlan;

    public static HarvestingError ToHarvestingError(ArtifactPlannerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.AsT0.Value;
    }

    public static DeploymentPlan ToDeploymentPlan(ArtifactPlannerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.AsT1.Value;
    }

    public DeploymentPlan ArtifactPlan => SuccessValue();

    public ArtifactPlannerError AsArtifactPlannerError() => (ArtifactPlannerError)AsT0.Value;
}

public static class ArtifactPlannerResultExtensions
{
    public static Result<HarvestingError, DeploymentPlan> ToResult(this ArtifactPlannerResult self) => self;

    [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks")]
    public static async Task<Result<HarvestingError, DeploymentPlan>> ToResult(this Task<ArtifactPlannerResult> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        var cr = await self.ConfigureAwait(false);
        return cr;
    }

    public static void ThrowIfError(this ArtifactPlannerResult result, Action<HarvestingError> errorHandler)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(errorHandler);

        if (result.IsError())
        {
            errorHandler(result.AsT0.Value);
        }
    }
}
