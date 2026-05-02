using Build.Features.Harvesting;
using Build.Shared.Manifest;
using Build.Shared.Runtime;

namespace Build.Shared.Strategy;

/// <summary>
/// Validates that satellite library closures conform to the hybrid-static packaging model.
/// <para>
/// In hybrid mode, a satellite library's closure should contain only:
/// <list type="bullet">
///   <item>Its own primary binaries (e.g., SDL2_image.dll)</item>
///   <item>The core SDL library (e.g., SDL2.dll) — allowed external dynamic dependency</item>
///   <item>System libraries (e.g., kernel32.dll, libc.so) — filtered by <see cref="IRuntimeProfile"/></item>
/// </list>
/// Any other binary in the closure is a transitive dependency leak — the static bake failed.
/// </para>
/// </summary>
public sealed class HybridStaticValidator(IRuntimeProfile profile, IPackagingStrategy strategy, ValidationMode mode) : IDependencyPolicyValidator
{
    private readonly IRuntimeProfile _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    private readonly IPackagingStrategy _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
    private readonly ValidationMode _mode = mode;

    /// <inheritdoc />
    public ValidationResult Validate(BinaryClosure closure, LibraryManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(closure);
        ArgumentNullException.ThrowIfNull(manifest);

        if (_mode == ValidationMode.Off)
        {
            return ValidationResult.Pass(_mode);
        }

        // Core libraries have no policy constraints — they are the root dynamic library
        if (manifest.IsCoreLib)
        {
            return ValidationResult.Pass(_mode);
        }

        // For satellite libraries in hybrid mode:
        // Every non-system, non-core, non-primary binary in the closure = transitive dep leak
        var violations = closure.Nodes
            .Where(node =>
                !_profile.IsSystemFile(node.Path.GetFilename().FullPath)
                && !_strategy.IsCoreLibrary(node.OwnerPackage)
                && !closure.IsPrimaryFile(node.Path))
            .ToList();

        if (violations.Count == 0)
        {
            return ValidationResult.Pass(_mode);
        }

        return _mode switch
        {
            ValidationMode.Strict => ValidationResult.Fail(violations),
            ValidationMode.Warn => ValidationResult.PassWithWarnings(violations, _mode),
            _ => ValidationResult.Pass(_mode),
        };
    }
}
