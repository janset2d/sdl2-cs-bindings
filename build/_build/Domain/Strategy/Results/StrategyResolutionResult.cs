using Build.Domain.Strategy.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Domain.Strategy.Results;

public sealed class StrategyResolutionResult(OneOf<Error<StrategyResolutionError>, Success<PackagingModel>> result)
    : Result<StrategyResolutionError, PackagingModel>(result)
{
    public static implicit operator StrategyResolutionResult(StrategyResolutionError error) => new(new Error<StrategyResolutionError>(error));
    public static implicit operator StrategyResolutionResult(PackagingModel resolvedModel) => new(new Success<PackagingModel>(resolvedModel));

    public static explicit operator StrategyResolutionError(StrategyResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static explicit operator PackagingModel(StrategyResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public static StrategyResolutionResult FromStrategyResolutionError(StrategyResolutionError error) => error;
    public static StrategyResolutionResult FromPackagingModel(PackagingModel resolvedModel) => resolvedModel;

    public static StrategyResolutionError ToStrategyResolutionError(StrategyResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static PackagingModel ToPackagingModel(StrategyResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public StrategyResolutionError ResolutionError => AsT0.Value;

    public PackagingModel ResolvedModel => SuccessValue();
}
