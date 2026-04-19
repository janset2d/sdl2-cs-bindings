using Build.Domain.Preflight.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Domain.Preflight.Results;

public sealed class CoreLibraryIdentityResult(OneOf<Error<CoreLibraryIdentityError>, Success<CoreLibraryIdentitySuccess>> result)
    : Result<CoreLibraryIdentityError, CoreLibraryIdentitySuccess>(result)
{
    public static implicit operator CoreLibraryIdentityResult(CoreLibraryIdentityError error) => new(new Error<CoreLibraryIdentityError>(error));

    public static implicit operator CoreLibraryIdentityResult(CoreLibraryIdentitySuccess success) => new(new Success<CoreLibraryIdentitySuccess>(success));

    public static explicit operator CoreLibraryIdentityError(CoreLibraryIdentityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static explicit operator CoreLibraryIdentitySuccess(CoreLibraryIdentityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public CoreLibraryIdentityValidation Validation => IsError()
        ? ErrorValue().Validation
        : SuccessValue().Validation;

    public CoreLibraryIdentitySuccess CoreLibraryIdentitySuccess => SuccessValue();

    public CoreLibraryIdentityError CoreLibraryIdentityError => AsT0.Value;

    public static CoreLibraryIdentityResult FromCoreLibraryIdentityError(CoreLibraryIdentityError error) => error;

    public static CoreLibraryIdentityResult FromCoreLibraryIdentitySuccess(CoreLibraryIdentitySuccess success) => success;

    public static CoreLibraryIdentityError ToCoreLibraryIdentityError(CoreLibraryIdentityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static CoreLibraryIdentitySuccess ToCoreLibraryIdentitySuccess(CoreLibraryIdentityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public static CoreLibraryIdentityResult Pass(CoreLibraryIdentityValidation validation) => new CoreLibraryIdentitySuccess(validation);

    public static CoreLibraryIdentityResult Fail(CoreLibraryIdentityValidation validation, string? message = null)
    {
        return new CoreLibraryIdentityError(
            validation,
            message ?? validation.Check.ErrorMessage ?? "Core library identity validation failed.");
    }
}

public sealed record CoreLibraryIdentitySuccess(CoreLibraryIdentityValidation Validation);

public sealed class CoreLibraryIdentityError : PreflightError
{
    public CoreLibraryIdentityError(CoreLibraryIdentityValidation validation, string message, Exception? exception = null)
        : base(message, exception)
    {
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
    }

    public CoreLibraryIdentityValidation Validation { get; }
}
