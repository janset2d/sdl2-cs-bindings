using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Targets.GenerateBindings.Model;
using Cake.Core;

namespace Build.Validation.BindingGeneration;

/// <summary>
/// Cross-checks emitted binding symbol names against SDL2's dynapi manifest
/// (<c>src/dynapi/SDL2.exports</c>). Family-scoped sibling of the older
/// <see cref="BindingPublicApiCoherenceValidator"/> — same logical check, fits
/// the <see cref="IBindingFamilyValidator"/> dispatch model that
/// <see cref="Build.Targets.GenerateBindings.GenerateBindingsTask"/> migrates
/// to in Phase 2D. The older validator is retained until that migration lands;
/// the two coexist by design during the transition.
/// <para>
/// Returns <see cref="ValidationReport.Empty"/> when <see cref="BindingGenerationConfig.Dynapi"/>
/// is null — that's the structural opt-out for families without a dynapi manifest
/// (SDL2 satellites + SDL3 retired the dynapi system entirely). The manifest's
/// <c>binding_generation.validators["dynapi-coherence"]</c> flag is a separate
/// per-family opt-in checked by the dispatch loop in
/// <see cref="Build.Targets.GenerateBindings.GenerateBindingsTask"/>.
/// </para>
/// </summary>
public sealed class DynapiCoherenceValidator(IDynapiManifestRepository dynapiRepo) : IBindingFamilyValidator
{
    private readonly IDynapiManifestRepository _dynapiRepo = dynapiRepo ?? throw new ArgumentNullException(nameof(dynapiRepo));

    private const string FalsePositiveCheckName = "Emitted binding references unexported symbol";
    private const string FalseNegativeCheckName = "Public SDL2 export missing from emitted bindings";

    public string ValidatorId => "dynapi-coherence";

    public async Task<ValidationReport> ValidateAsync(PreviewBindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(config);

        if (config.Dynapi is null)
        {
            return ValidationReport.Empty;
        }

        var manifestResult = await _dynapiRepo.LoadAsync(ct).ConfigureAwait(false);
        if (manifestResult.TryGetError(out var err))
        {
            throw new CakeException(err.Reason);
        }

        var manifest = manifestResult.Value;
        var emittedSymbols = model.Views
            .SelectMany(v => v.Functions)
            .Select(f => f.Name)
            .ToHashSet(StringComparer.Ordinal);

        if (emittedSymbols.Count == 0 && manifest.PublicSymbols.Count == 0)
        {
            return ValidationReport.Empty;
        }

        var profile = SeverityProfile.Stage1Generator;
        var checks = new List<ValidationCheck>();

        foreach (var emitted in emittedSymbols.OrderBy(static s => s, StringComparer.Ordinal))
        {
            if (manifest.PublicSymbols.Contains(emitted)) continue;
            checks.Add(new ValidationCheck(
                Name: FalsePositiveCheckName,
                Severity: profile.FalsePositiveSeverity,
                Message: $"Binding emits P/Invoke for '{emitted}' but SDL2's dynapi manifest does not list it as a public export (source: {manifest.Origin} '{manifest.SourcePath}'). Calling it would raise EntryPointNotFoundException; inspect translator inline filter / header exclusion list."));
        }

        foreach (var exported in manifest.PublicSymbols.OrderBy(static s => s, StringComparer.Ordinal))
        {
            if (emittedSymbols.Contains(exported)) continue;
            checks.Add(new ValidationCheck(
                Name: FalseNegativeCheckName,
                Severity: profile.FalseNegativeSeverity,
                Message: $"SDL2 dynapi manifest lists '{exported}' as a public export but the generator did not emit it (source: {manifest.Origin} '{manifest.SourcePath}'). Likely cause: header exclusion list too aggressive, parse-view defines missing a platform, or AST inline filter over-classifying."));
        }

        return new ValidationReport(checks);
    }
}
