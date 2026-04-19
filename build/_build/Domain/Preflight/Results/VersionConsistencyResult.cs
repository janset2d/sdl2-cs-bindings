using Build.Domain.Preflight.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Domain.Preflight.Results;

public sealed class VersionConsistencyResult(OneOf<Error<VersionConsistencyError>, Success<VersionConsistencySuccess>> result)
    : Result<VersionConsistencyError, VersionConsistencySuccess>(result)
{
    public static implicit operator VersionConsistencyResult(VersionConsistencyError error) => new(new Error<VersionConsistencyError>(error));

    public static implicit operator VersionConsistencyResult(VersionConsistencySuccess success) => new(new Success<VersionConsistencySuccess>(success));

    public static explicit operator VersionConsistencyError(VersionConsistencyResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static explicit operator VersionConsistencySuccess(VersionConsistencyResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public VersionConsistencyValidation Validation => IsError()
        ? ErrorValue().Validation
        : SuccessValue().Validation;

    public VersionConsistencySuccess VersionConsistencySuccess => SuccessValue();

    public VersionConsistencyError VersionConsistencyError => AsT0.Value;

    public static VersionConsistencyResult FromVersionConsistencyError(VersionConsistencyError error) => error;

    public static VersionConsistencyResult FromVersionConsistencySuccess(VersionConsistencySuccess success) => success;

    public static VersionConsistencyError ToVersionConsistencyError(VersionConsistencyResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static VersionConsistencySuccess ToVersionConsistencySuccess(VersionConsistencyResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public static VersionConsistencyResult Pass(VersionConsistencyValidation validation) => new VersionConsistencySuccess(validation);

    public static VersionConsistencyResult Fail(VersionConsistencyValidation validation, string? message = null)
    {
        return new VersionConsistencyError(
            validation,
            message ?? $"Version consistency validation failed: {validation.Checks.Count(check => check.IsError)} error(s) detected.");
    }
}

public sealed record VersionConsistencySuccess(VersionConsistencyValidation Validation);

public sealed class VersionConsistencyError : PreflightError
{
    public VersionConsistencyError(VersionConsistencyValidation validation, string message, Exception? exception = null)
        : base(message, exception)
    {
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
    }

    public VersionConsistencyValidation Validation { get; }
}