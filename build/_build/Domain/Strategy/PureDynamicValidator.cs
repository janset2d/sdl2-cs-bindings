using Build.Context.Models;
using Build.Domain.Harvesting.Models;
using Build.Domain.Strategy.Models;
using Build.Domain.Strategy.Results;

namespace Build.Domain.Strategy;

/// <summary>
/// Pass-through validator for pure-dynamic mode.
/// In this model, transitive dependencies are expected and therefore never treated as violations.
/// </summary>
public sealed class PureDynamicValidator(ValidationMode mode) : IDependencyPolicyValidator
{
    private readonly ValidationMode _mode = mode;

    /// <inheritdoc />
    public ValidationResult Validate(BinaryClosure closure, LibraryManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(closure);
        ArgumentNullException.ThrowIfNull(manifest);

        return ValidationResult.Pass(_mode);
    }
}
