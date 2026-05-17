using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Targets.GenerateBindings.Model;

namespace Build.Validation.BindingGeneration;

/// <summary>
/// Fail-closed on a missing or empty Neutral parse view. The Neutral view is
/// the platform-agnostic baseline; if it has zero functions, either the header
/// set resolved to nothing or the platform macro hygiene is misconfigured and
/// every function got attributed to a platform-only view. Both are structural
/// regressions — without Neutral functions, every consumer's basic SDL_Init /
/// SDL_Quit call path is unreachable.
/// </summary>
public sealed class NeutralViewNonEmptyValidator : IBindingFamilyValidator
{
    public string ValidatorId => "neutral-view-non-empty";

    public Task<ValidationReport> ValidateAsync(BindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(config);

        var neutral = model.Views.FirstOrDefault(v => string.Equals(v.Name, "Neutral", StringComparison.Ordinal));
        var checks = new List<ValidationCheck>();

        if (neutral is null)
        {
            checks.Add(new ValidationCheck(
                Name: "Neutral parse view missing",
                Severity: ValidationSeverity.Error,
                Message: $"Family '{config.FamilyId}' has no Neutral parse view in the binding model. Platform catalog misconfigured — every PlatformCatalog must declare a Neutral entry first so platform-overlay views can subtract from it."));
        }
        else if (neutral.Functions.Count == 0)
        {
            checks.Add(new ValidationCheck(
                Name: "Neutral parse view empty",
                Severity: ValidationSeverity.Error,
                Message: $"Family '{config.FamilyId}' Neutral parse view returned 0 functions. Header set or platform macro hygiene likely misconfigured. Inspect HeaderSetResolver exclusions and PlatformCatalog.AllPlatformMacros."));
        }

        return Task.FromResult(new ValidationReport(checks));
    }
}
