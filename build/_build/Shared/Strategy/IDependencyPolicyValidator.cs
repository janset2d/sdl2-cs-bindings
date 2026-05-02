using Build.Features.Harvesting;
using Build.Shared.Manifest;

namespace Build.Shared.Strategy;

/// <summary>
/// Validates whether a <see cref="BinaryClosure"/> conforms to the active packaging strategy.
/// In hybrid-static mode, any transitive dependency that appears as a separate dynamic library
/// (beyond system files and the core SDL library) is a policy violation — it means the static
/// bake failed and a dependency leaked.
/// </summary>
public interface IDependencyPolicyValidator
{
    /// <summary>
    /// Validates the closure against the packaging policy for the given library.
    /// </summary>
    /// <param name="closure">The binary dependency closure produced by <see cref="IBinaryClosureWalker"/>.</param>
    /// <param name="manifest">The library manifest entry being validated.</param>
    /// <returns>A <see cref="ValidationResult"/> — success (possibly with warnings) or error with violations.</returns>
    ValidationResult Validate(BinaryClosure closure, LibraryManifest manifest);
}
