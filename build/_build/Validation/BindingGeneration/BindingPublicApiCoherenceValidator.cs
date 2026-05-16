using Build.Data.BindingGeneration.Models;
using Build.Results;

namespace Build.Validation.BindingGeneration;

/// <summary>
/// Cross-checks emitted binding symbol names against SDL2's dynapi manifest
/// (<c>src/dynapi/SDL2.exports</c>). Surfaces two failure modes:
/// <list type="bullet">
///   <item><description><b>False-positive</b> (emit \ manifest) — the generator
///   emitted a P/Invoke for a symbol the SDL2 shared library does not export.
///   Calling it raises <c>EntryPointNotFoundException</c> at consumer runtime.
///   Typical cause: an SDL_FORCE_INLINE or static-inline helper leaked through
///   the AST filter.</description></item>
///   <item><description><b>False-negative</b> (manifest \ emit) — the generator
///   skipped a symbol the SDL2 library does export. Typical cause: header
///   exclusion list too aggressive, parse view missing a platform define, or
///   the AST inline filter over-classifying.</description></item>
/// </list>
/// Reused across Stage 1 GenerateBindings (post-emit, Stage1Generator severity)
/// and Stage 2 PreFlight / Pack (Stage2Strict severity) per
/// <see cref="SeverityProfile"/>.
/// </summary>
public interface IBindingPublicApiCoherenceValidator
{
    ValidationReport Validate(IReadOnlySet<string> emittedSymbols, DynapiManifest manifest, SeverityProfile profile);
}

/// <inheritdoc />
public sealed class BindingPublicApiCoherenceValidator : IBindingPublicApiCoherenceValidator
{
    private const string FalsePositiveCheckName = "Emitted binding references unexported symbol";
    private const string FalseNegativeCheckName = "Public SDL2 export missing from emitted bindings";

    public ValidationReport Validate(IReadOnlySet<string> emittedSymbols, DynapiManifest manifest, SeverityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(emittedSymbols);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(profile);

        if (emittedSymbols.Count == 0 && manifest.PublicSymbols.Count == 0)
        {
            return ValidationReport.Empty;
        }

        var checks = new List<ValidationCheck>();

        foreach (var emitted in emittedSymbols.OrderBy(static s => s, StringComparer.Ordinal))
        {
            if (manifest.PublicSymbols.Contains(emitted))
            {
                continue;
            }

            checks.Add(new ValidationCheck(
                Name: FalsePositiveCheckName,
                Severity: profile.FalsePositiveSeverity,
                Message: $"Binding emits P/Invoke for '{emitted}' but SDL2's dynapi manifest does not list it as a public export (source: {manifest.Origin} '{manifest.SourcePath}'). Calling it would raise EntryPointNotFoundException; inspect translator inline filter / header exclusion list."));
        }

        foreach (var exported in manifest.PublicSymbols.OrderBy(static s => s, StringComparer.Ordinal))
        {
            if (emittedSymbols.Contains(exported))
            {
                continue;
            }

            checks.Add(new ValidationCheck(
                Name: FalseNegativeCheckName,
                Severity: profile.FalseNegativeSeverity,
                Message: $"SDL2 dynapi manifest lists '{exported}' as a public export but the generator did not emit it (source: {manifest.Origin} '{manifest.SourcePath}'). Likely cause: header exclusion list too aggressive, parse-view defines missing a platform, or AST inline filter over-classifying."));
        }

        return new ValidationReport(checks);
    }
}
