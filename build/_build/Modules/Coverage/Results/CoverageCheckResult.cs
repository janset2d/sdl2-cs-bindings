using System.Diagnostics.CodeAnalysis;
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

public static class CoverageCheckResultExtensions
{
    public static Result<CoverageError, CoverageCheckSuccess> ToResult(this CoverageCheckResult self) => self;

    [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks")]
    public static async Task<Result<CoverageError, CoverageCheckSuccess>> ToResult(this Task<CoverageCheckResult> self)
    {
        ArgumentNullException.ThrowIfNull(self);
        var cr = await self.ConfigureAwait(false);
        return cr;
    }

    /// <summary>
    /// Invokes <paramref name="errorHandler"/> if the result represents an error.
    /// The handler is expected to log/throw as appropriate — this method does not throw on its own,
    /// matching the pattern established by <c>ValidationResult.ThrowIfError</c> and
    /// <c>ClosureResult.ThrowIfError</c>.
    /// </summary>
    public static void ThrowIfError(this CoverageCheckResult result, Action<CoverageError> errorHandler)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(errorHandler);

        if (result.IsError())
        {
            errorHandler(result.CoverageError);
        }
    }
}
