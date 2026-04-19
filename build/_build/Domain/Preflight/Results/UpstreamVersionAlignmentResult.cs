using Build.Domain.Preflight.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Domain.Preflight.Results;

public sealed class UpstreamVersionAlignmentResult(OneOf<Error<UpstreamVersionAlignmentError>, Success<UpstreamVersionAlignmentSuccess>> result)
    : Result<UpstreamVersionAlignmentError, UpstreamVersionAlignmentSuccess>(result)
{
    public static implicit operator UpstreamVersionAlignmentResult(UpstreamVersionAlignmentError error) => new(new Error<UpstreamVersionAlignmentError>(error));

    public static implicit operator UpstreamVersionAlignmentResult(UpstreamVersionAlignmentSuccess success) => new(new Success<UpstreamVersionAlignmentSuccess>(success));

    public static explicit operator UpstreamVersionAlignmentError(UpstreamVersionAlignmentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static explicit operator UpstreamVersionAlignmentSuccess(UpstreamVersionAlignmentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public UpstreamVersionAlignmentValidation Validation => IsError()
        ? ErrorValue().Validation
        : SuccessValue().Validation;

    public UpstreamVersionAlignmentSuccess UpstreamVersionAlignmentSuccess => SuccessValue();

    public UpstreamVersionAlignmentError UpstreamVersionAlignmentError => AsT0.Value;

    public static UpstreamVersionAlignmentResult FromUpstreamVersionAlignmentError(UpstreamVersionAlignmentError error) => error;

    public static UpstreamVersionAlignmentResult FromUpstreamVersionAlignmentSuccess(UpstreamVersionAlignmentSuccess success) => success;

    public static UpstreamVersionAlignmentError ToUpstreamVersionAlignmentError(UpstreamVersionAlignmentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static UpstreamVersionAlignmentSuccess ToUpstreamVersionAlignmentSuccess(UpstreamVersionAlignmentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public static UpstreamVersionAlignmentResult Pass(UpstreamVersionAlignmentValidation validation) => new UpstreamVersionAlignmentSuccess(validation);

    public static UpstreamVersionAlignmentResult Fail(UpstreamVersionAlignmentValidation validation, string? message = null)
    {
        return new UpstreamVersionAlignmentError(
            validation,
            message ?? $"Upstream version alignment validation failed: {validation.Checks.Count(check => check.IsError)} error(s) detected.");
    }
}

public sealed record UpstreamVersionAlignmentSuccess(UpstreamVersionAlignmentValidation Validation);

public sealed class UpstreamVersionAlignmentError : PreflightError
{
    public UpstreamVersionAlignmentError(UpstreamVersionAlignmentValidation validation, string message, Exception? exception = null)
        : base(message, exception)
    {
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
    }

    public UpstreamVersionAlignmentValidation Validation { get; }
}
