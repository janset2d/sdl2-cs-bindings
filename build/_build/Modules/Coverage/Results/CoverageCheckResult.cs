using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Modules.Coverage.Results;

/// <summary>
/// Result monad for coverage ratchet validation:
/// <list type="bullet">
///   <item><term>Success</term><description><see cref="CoverageCheckSuccess"/> — coverage met or exceeded the baseline floor</description></item>
///   <item><term>Error</term><description><see cref="CoverageError"/> — baseline violated (typically <see cref="CoverageThresholdViolation"/>)</description></item>
/// </list>
/// Mirrors the <c>ValidationResult</c> / <c>ClosureResult</c> pattern used by Strategy and Harvesting.
/// </summary>
public sealed class CoverageCheckResult(OneOf<Error<CoverageError>, Success<CoverageCheckSuccess>> result)
    : Result<CoverageError, CoverageCheckSuccess>(result)
{
    public static implicit operator CoverageCheckResult(CoverageError error) => new(new Error<CoverageError>(error));
    public static implicit operator CoverageCheckResult(CoverageCheckSuccess success) => new(new Success<CoverageCheckSuccess>(success));

    public static explicit operator CoverageError(CoverageCheckResult _)
    {
        ArgumentNullException.ThrowIfNull(_);
        return _.AsT0.Value;
    }

    public static explicit operator CoverageCheckSuccess(CoverageCheckResult _)
    {
        ArgumentNullException.ThrowIfNull(_);
        return _.AsT1.Value;
    }

    public static CoverageCheckResult FromCoverageError(CoverageError error) => error;
    public static CoverageCheckResult FromCoverageCheckSuccess(CoverageCheckSuccess success) => success;

    public static CoverageError ToCoverageError(CoverageCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static CoverageCheckSuccess ToCoverageCheckSuccess(CoverageCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    /// <summary>The successful ratchet result.</summary>
    public CoverageCheckSuccess CheckSuccess => SuccessValue();

    /// <summary>The ratchet error, if any.</summary>
    public CoverageError CoverageError => AsT0.Value;
}
