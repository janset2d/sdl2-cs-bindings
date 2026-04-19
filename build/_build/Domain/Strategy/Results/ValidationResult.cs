using Build.Domain.Harvesting.Models;
using Build.Domain.Strategy.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Domain.Strategy.Results;

/// <summary>
/// Result monad for dependency policy validation:
/// <list type="bullet">
///   <item><term>Success</term><description><see cref="ValidationSuccess"/> — validation passed (may contain warnings)</description></item>
///   <item><term>Error</term><description><see cref="ValidationError"/> — validation failed (Strict mode violations)</description></item>
/// </list>
/// </summary>
public sealed class ValidationResult(OneOf<Error<ValidationError>, Success<ValidationSuccess>> result)
    : Result<ValidationError, ValidationSuccess>(result)
{
    public static implicit operator ValidationResult(ValidationError error) => new(new Error<ValidationError>(error));
    public static implicit operator ValidationResult(ValidationSuccess success) => new(new Success<ValidationSuccess>(success));

    public static explicit operator ValidationError(ValidationResult _)
    {
        ArgumentNullException.ThrowIfNull(_);
        return _.AsT0.Value;
    }

    public static explicit operator ValidationSuccess(ValidationResult _)
    {
        ArgumentNullException.ThrowIfNull(_);
        return _.AsT1.Value;
    }

    public static ValidationResult FromValidationError(ValidationError error) => error;
    public static ValidationResult FromValidationSuccess(ValidationSuccess success) => success;

    public static ValidationError ToValidationError(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static ValidationSuccess ToValidationSuccess(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    /// <summary>The successful validation result.</summary>
    public ValidationSuccess ValidationSuccess => SuccessValue();

    /// <summary>The validation error.</summary>
    public ValidationError ValidationError => AsT0.Value;

    /// <summary>Creates a passing result with no warnings.</summary>
    public static ValidationResult Pass(ValidationMode mode = ValidationMode.Strict)
    {
        return new ValidationSuccess(mode);
    }

    /// <summary>Creates a passing result with non-blocking warnings (Warn mode).</summary>
    public static ValidationResult PassWithWarnings(IReadOnlyList<BinaryNode> warnings, ValidationMode mode)
    {
        return new ValidationSuccess(mode, warnings);
    }

    /// <summary>Creates a failing result with violations (Strict mode).</summary>
    public static ValidationResult Fail(IReadOnlyList<BinaryNode> violations, string? message = null)
    {
        var msg = message ?? $"Dependency policy validation failed: {violations.Count} violation(s) detected.";
        return new ValidationError(msg, violations);
    }
}
