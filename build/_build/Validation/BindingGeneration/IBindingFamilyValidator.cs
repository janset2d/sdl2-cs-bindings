using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Targets.GenerateBindings.Model;

namespace Build.Validation.BindingGeneration;

/// <summary>
/// Family-scoped post-emit validator. Each implementation owns one
/// <see cref="ValidatorId"/> that maps to a boolean flag under
/// <c>build/manifest.json library_manifests[].binding_generation.validators</c>;
/// <see cref="Build.Targets.GenerateBindings.GenerateBindingsTask"/> filters
/// the registered <see cref="IEnumerable{T}"/> by the manifest-enabled subset
/// per family per run.
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
    /// Adding a new validator means: implement this interface, register against
    /// it in DI, flip its key to <c>true</c> in the relevant family's manifest
    /// block.
    /// </summary>
    string ValidatorId { get; }

    Task<ValidationReport> ValidateAsync(BindingModel model, BindingGenerationConfig config, CancellationToken ct);
}
