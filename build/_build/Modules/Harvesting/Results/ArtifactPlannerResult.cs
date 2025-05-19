using System.Diagnostics.CodeAnalysis;
using Build.Modules.Harvesting.Models;
using Cake.Core;
using Cake.Core.Diagnostics;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Modules.Harvesting.Results;

public sealed class ArtifactPlannerResult(OneOf<Error<HarvestingError>, Success<ArtifactPlan>> result) : Result<HarvestingError, ArtifactPlan>(result)
{
    public static implicit operator ArtifactPlannerResult(HarvestingError error) => new(new Error<HarvestingError>(error));
    public static implicit operator ArtifactPlannerResult(ArtifactPlan artifactPlan) => new(new Success<ArtifactPlan>(artifactPlan));

    public static explicit operator HarvestingError(ArtifactPlannerResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT0.Value;
    }

    public static explicit operator ArtifactPlan(ArtifactPlannerResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT1.Value;
    }

    public static ArtifactPlannerResult FromHarvestingError(HarvestingError error) => error;
    public static ArtifactPlannerResult FromArtifactPlan(ArtifactPlan artifactPlan) => artifactPlan;

    public static HarvestingError ToHarvestingError(ArtifactPlannerResult result)
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

    public ArtifactPlannerError AsArtifactPlannerError() => (ArtifactPlannerError)AsT0.Value;
}

public static class ArtifactPlannerResultExtensions
{
    public static Result<HarvestingError, ArtifactPlan> ToResult(this ArtifactPlannerResult self) => self;

    [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks")]
    public static async Task<Result<HarvestingError, ArtifactPlan>> ToResult(this Task<ArtifactPlannerResult> self)
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
