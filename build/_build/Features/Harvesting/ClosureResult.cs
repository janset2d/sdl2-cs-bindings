using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Features.Harvesting;

/// <summary>
/// Result monad variant of <see cref="ClosureResult"/>:
/// <list type="bullet">
///   <item><term>Success</term><description><see cref="BinaryClosure"/></description></item>
///   <item><term>Error</term><description><see cref="HarvestingError"/></description></item>
/// </list>
/// </summary>
public sealed class ClosureResult(OneOf<Error<HarvestingError>, Success<BinaryClosure>> result) : Result<HarvestingError, BinaryClosure>(result)
{
    public static implicit operator ClosureResult(HarvestingError error) => new(new Error<HarvestingError>(error));
    public static implicit operator ClosureResult(BinaryClosure closure) => new(new Success<BinaryClosure>(closure));

    public static explicit operator HarvestingError(ClosureResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT0.Value;
    }

    public static explicit operator BinaryClosure(ClosureResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT1.Value;
    }

    public static ClosureResult FromHarvestingError(HarvestingError error) => error;
    public static ClosureResult FromBinaryClosure(BinaryClosure closure) => closure;

    public static HarvestingError ToHarvestingError(ClosureResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.AsT0.Value;
    }

    public static BinaryClosure ToBinaryClosure(ClosureResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.AsT1.Value;
    }

    public BinaryClosure Closure => SuccessValue();
    public ClosureError AsClosureError() => (ClosureError)AsT0.Value;
}
