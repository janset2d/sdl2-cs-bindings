using Build.Context.Models;
using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Strategy.Models;
using Build.Modules.Strategy.Results;

namespace Build.Modules.Strategy;

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
