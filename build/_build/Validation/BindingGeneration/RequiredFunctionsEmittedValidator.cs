using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Targets.GenerateBindings.Model;

namespace Build.Validation.BindingGeneration;

/// <summary>
/// Asserts that every function declared in
/// <see cref="BindingGenerationConfig.RequiredFunctions"/> made it into the
/// Neutral parse view of the emitted model.
/// <para>
/// The required-functions list is the hand-curated fallback for symbols the
/// per-header parse loop cannot reach (SDL.h-only base API like
/// <c>SDL_Init</c> / <c>SDL_Quit</c>, where the umbrella header is excluded
/// from parsing). If one of those entries goes missing from emit, the
/// translator's required-functions injection is broken — silent regression
/// of the most-called public API.
/// </para>
/// </summary>
public sealed class RequiredFunctionsEmittedValidator : IBindingFamilyValidator
{
    public string ValidatorId => "required-functions-emitted";

    public Task<ValidationReport> ValidateAsync(PreviewBindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(config);

        if (config.RequiredFunctions.Count == 0)
        {
            return Task.FromResult(ValidationReport.Empty);
        }

        var neutral = model.Views.FirstOrDefault(v => string.Equals(v.Name, "Neutral", StringComparison.Ordinal));
        var emittedInNeutral = neutral?.Functions.Select(f => f.Name).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);

        var checks = new List<ValidationCheck>();
        foreach (var required in config.RequiredFunctions.OrderBy(rf => rf.Name, StringComparer.Ordinal))
        {
            if (emittedInNeutral.Contains(required.Name)) continue;
            checks.Add(new ValidationCheck(
                Name: "Required function not emitted in Neutral view",
                Severity: ValidationSeverity.Error,
                Message: $"Family '{config.FamilyId}' declares required function '{required.Name}' from {required.SourceHeader} but it was not emitted in the Neutral view. Either the translator's required-functions injection is broken, the header exclusion list is too aggressive, or the function should be removed from binding_generation.required_functions."));
        }

        return Task.FromResult(new ValidationReport(checks));
    }
}
