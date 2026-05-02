using Build.Shared.Harvesting;
using Build.Shared.Results;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Features.Harvesting;

public sealed class CopierResult(OneOf<Error<HarvestingError>, Success<Unit>> result) : Result<HarvestingError, Unit>(result)
{
    public static implicit operator CopierResult(HarvestingError error) => new(new Error<HarvestingError>(error));
    public static implicit operator CopierResult(Unit unit) => new(new Success<Unit>(unit));

    public static explicit operator HarvestingError(CopierResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT0.Value;
    }

    public static explicit operator Unit(CopierResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT1.Value;
    }

    public static CopierResult FromHarvestingError(HarvestingError error) => error;
    public static CopierResult FromUnit(Unit unit) => unit;

    public static HarvestingError ToHarvestingError(CopierResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.AsT0.Value;
    }

    public static Unit ToUnit(CopierResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.AsT1.Value;
    }

    public static CopierResult ToSuccess() => Unit.Value;

    public CopierError AsCopierError() => (CopierError)AsT0.Value;
}
