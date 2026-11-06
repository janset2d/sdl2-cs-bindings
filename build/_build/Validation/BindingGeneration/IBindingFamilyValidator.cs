using Build.Data.BindingGeneration.Models;
using Build.Results;

namespace Build.Validation.BindingGeneration;

/// <summary>
/// Family-scoped native-symbol validator. Each implementation owns one
/// <see cref="ValidatorId"/> that maps to a boolean flag under
/// <c>build/manifest.json library_manifests[].binding_generation.validators</c>.
/// <para>
/// Pre-emit / lifecycle-stage validators (PreFlight stamp drift, Pack-stage
/// symbol existence) are separate concerns — they validate at different points
/// in the pipeline, not per-family during generation. They use the existing
/// <c>ValidationReport</c> contract directly without going through this
/// interface.
/// </para>
/// </summary>
public interface IBindingFamilyValidator
{
    /// <summary>
    /// Stable kebab-case identifier matching the key under
    /// <c>binding_generation.validators</c> in <c>build/manifest.json</c>.
    /// </summary>
    public string ValidatorId { get; }

    public Task<ValidationReport> ValidateAsync(IReadOnlySet<string> emittedEntryPoints, BindingGenerationConfig config, CancellationToken ct);
}
