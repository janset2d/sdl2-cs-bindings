using Build.Modules.Harvesting.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Modules.Harvesting.Results;

public sealed class ArtifactPlannerResult(OneOf<Error<ArtifactPlannerError>, Success<ArtifactPlan>> result) : Result<ArtifactPlannerError, ArtifactPlan>(result)
{
    public static implicit operator ArtifactPlannerResult(ArtifactPlannerError error) => new(new Error<ArtifactPlannerError>(error));
    public static implicit operator ArtifactPlannerResult(ArtifactPlan artifactPlan) => new(new Success<ArtifactPlan>(artifactPlan));

    public static explicit operator ArtifactPlannerError(ArtifactPlannerResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT0.Value;
    }

    public static explicit operator ArtifactPlan(ArtifactPlannerResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT1.Value;
    }

    public static ArtifactPlannerResult FromArtifactPlannerError(ArtifactPlannerError error) => error;
    public static ArtifactPlannerResult FromArtifactPlan(ArtifactPlan artifactPlan) => artifactPlan;

    public static ArtifactPlannerError ToArtifactPlannerError(ArtifactPlannerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.AsT0.Value;
    }

    public static ArtifactPlan ToArtifactPlan(ArtifactPlannerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.AsT1.Value;
    }

    public ArtifactPlan ArtifactPlan => SuccessValue();
}
