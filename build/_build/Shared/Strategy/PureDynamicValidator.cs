using Build.Shared.Harvesting;
using Build.Shared.Manifest;

namespace Build.Shared.Strategy;

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
