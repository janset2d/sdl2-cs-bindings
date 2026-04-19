using Build.Domain.Preflight.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Domain.Preflight.Results;

public sealed class StrategyCoherenceResult(OneOf<Error<StrategyCoherenceError>, Success<StrategyCoherenceSuccess>> result)
    : Result<StrategyCoherenceError, StrategyCoherenceSuccess>(result)
{
    public static implicit operator StrategyCoherenceResult(StrategyCoherenceError error) => new(new Error<StrategyCoherenceError>(error));

    public static implicit operator StrategyCoherenceResult(StrategyCoherenceSuccess success) => new(new Success<StrategyCoherenceSuccess>(success));

    public static explicit operator StrategyCoherenceError(StrategyCoherenceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static explicit operator StrategyCoherenceSuccess(StrategyCoherenceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public StrategyCoherenceValidation Validation => IsError()
        ? ErrorValue().Validation
        : SuccessValue().Validation;

    public StrategyCoherenceSuccess StrategyCoherenceSuccess => SuccessValue();

    public StrategyCoherenceError StrategyCoherenceError => AsT0.Value;

    public static StrategyCoherenceResult FromStrategyCoherenceError(StrategyCoherenceError error) => error;

    public static StrategyCoherenceResult FromStrategyCoherenceSuccess(StrategyCoherenceSuccess success) => success;

    public static StrategyCoherenceError ToStrategyCoherenceError(StrategyCoherenceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static StrategyCoherenceSuccess ToStrategyCoherenceSuccess(StrategyCoherenceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public static StrategyCoherenceResult Pass(StrategyCoherenceValidation validation) => new StrategyCoherenceSuccess(validation);

    public static StrategyCoherenceResult Fail(StrategyCoherenceValidation validation, string? message = null)
    {
        return new StrategyCoherenceError(
            validation,
            message ?? $"Strategy coherence validation failed: {validation.Checks.Count(check => !check.IsValid)} error(s) detected.");
    }
}

public sealed record StrategyCoherenceSuccess(StrategyCoherenceValidation Validation);

public sealed class StrategyCoherenceError : PreflightError
{
    public StrategyCoherenceError(StrategyCoherenceValidation validation, string message, Exception? exception = null)
        : base(message, exception)
    {
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
    }

    public StrategyCoherenceValidation Validation { get; }
}