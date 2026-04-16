using Build.Modules.Preflight.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Modules.Preflight.Results;

public sealed class CsprojPackContractResult(OneOf<Error<CsprojPackContractError>, Success<CsprojPackContractSuccess>> result)
    : Result<CsprojPackContractError, CsprojPackContractSuccess>(result)
{
    public static implicit operator CsprojPackContractResult(CsprojPackContractError error) => new(new Error<CsprojPackContractError>(error));

    public static implicit operator CsprojPackContractResult(CsprojPackContractSuccess success) => new(new Success<CsprojPackContractSuccess>(success));

    public static explicit operator CsprojPackContractError(CsprojPackContractResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static explicit operator CsprojPackContractSuccess(CsprojPackContractResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public CsprojPackContractValidation Validation => IsError()
        ? ErrorValue().Validation
        : SuccessValue().Validation;

    public CsprojPackContractSuccess CsprojPackContractSuccess => SuccessValue();

    public CsprojPackContractError CsprojPackContractError => AsT0.Value;

    public static CsprojPackContractResult FromCsprojPackContractError(CsprojPackContractError error) => error;

    public static CsprojPackContractResult FromCsprojPackContractSuccess(CsprojPackContractSuccess success) => success;

    public static CsprojPackContractError ToCsprojPackContractError(CsprojPackContractResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static CsprojPackContractSuccess ToCsprojPackContractSuccess(CsprojPackContractResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public static CsprojPackContractResult Pass(CsprojPackContractValidation validation) => new CsprojPackContractSuccess(validation);

    public static CsprojPackContractResult Fail(CsprojPackContractValidation validation, string? message = null)
    {
        return new CsprojPackContractError(
            validation,
            message ?? $"Csproj pack contract validation failed: {validation.Checks.Count(check => check.IsError)} error(s) detected.");
    }
}

public sealed record CsprojPackContractSuccess(CsprojPackContractValidation Validation);

public sealed class CsprojPackContractError : PreflightError
{
    public CsprojPackContractError(CsprojPackContractValidation validation, string message, Exception? exception = null)
        : base(message, exception)
    {
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
    }

    public CsprojPackContractValidation Validation { get; }
}
