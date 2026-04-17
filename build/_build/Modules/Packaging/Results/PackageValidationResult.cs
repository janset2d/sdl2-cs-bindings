using Build.Modules.Packaging.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Modules.Packaging.Results;

/// <summary>
/// Result monad for <c>IPackageOutputValidator.ValidateAsync</c>:
/// <list type="bullet">
///   <item><term>Success</term><description><see cref="PackageValidationSuccess"/> — every post-S1 guardrail passed</description></item>
///   <item><term>Error</term><description><see cref="PackageValidationError"/> — one or more guardrails tripped; full check list preserved</description></item>
/// </list>
/// Mirrors the <see cref="Build.Modules.Preflight.Results.CsprojPackContractResult"/> pattern
/// (<c>Pass</c>/<c>Fail</c> factories + <see cref="Validation"/> accessor) so the task layer
/// can iterate violations instead of catching first-throw-wins exceptions.
/// </summary>
public sealed class PackageValidationResult(OneOf<Error<PackageValidationError>, Success<PackageValidationSuccess>> result)
    : Result<PackageValidationError, PackageValidationSuccess>(result)
{
    public static implicit operator PackageValidationResult(PackageValidationError error) => new(new Error<PackageValidationError>(error));
    public static implicit operator PackageValidationResult(PackageValidationSuccess success) => new(new Success<PackageValidationSuccess>(success));

    public static explicit operator PackageValidationError(PackageValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static explicit operator PackageValidationSuccess(PackageValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public static PackageValidationResult FromPackageValidationError(PackageValidationError error) => error;
    public static PackageValidationResult FromPackageValidationSuccess(PackageValidationSuccess success) => success;

    public static PackageValidationError ToPackageValidationError(PackageValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static PackageValidationSuccess ToPackageValidationSuccess(PackageValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    /// <summary>
    /// Full validation aggregate regardless of outcome — use this to surface per-check diagnostics.
    /// </summary>
    public PackageValidation Validation => IsError()
        ? ErrorValue().Validation
        : SuccessValue().Validation;

    public PackageValidationSuccess PackageValidationSuccess => SuccessValue();

    public PackageValidationError PackageValidationError => AsT0.Value;

    public static PackageValidationResult Pass(PackageValidation validation) => new PackageValidationSuccess(validation);

    public static PackageValidationResult Fail(PackageValidation validation, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(validation);
        return new PackageValidationError(
            validation,
            message ?? $"Package validation failed: {validation.Checks.Count(check => check.IsError)} guardrail(s) tripped.");
    }
}

public sealed record PackageValidationSuccess(PackageValidation Validation);

public sealed class PackageValidationError : PackagingError
{
    public PackageValidationError(PackageValidation validation, string message, Exception? exception = null)
        : base(message, exception)
    {
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
    }

    public PackageValidation Validation { get; }
}
