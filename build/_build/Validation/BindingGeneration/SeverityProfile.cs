using Build.Results;

namespace Build.Validation.BindingGeneration;

/// <summary>
/// Per-stage severity policy for <see cref="BindingPublicApiCoherenceValidator"/>.
/// Different stages of the binding ↔ native coherence pipeline want different
/// fail-closed behaviour:
/// <list type="bullet">
///   <item><description><b>Stage 1 GenerateBindings</b> — emit-time validation.
///   False-positives (sızıntı) are fail-closed (Error); false-negatives are
///   warnings (Warning) because Stage 1 iteratively tunes exclusion lists and
///   parse-view defines, and a transient under-emit can be expected during
///   that tuning.</description></item>
///   <item><description><b>Stage 2 PreFlight / Pack</b> — fully-baked binding +
///   native pairing. Both directions fail-closed.</description></item>
/// </list>
/// </summary>
public sealed record SeverityProfile(ValidationSeverity FalsePositiveSeverity, ValidationSeverity FalseNegativeSeverity)
{
    /// <summary>
    /// Stage 1 GenerateBindings task profile: fail-closed on bindings emitted
    /// for symbols the native does not export; warn on manifest symbols missing
    /// from the bindings.
    /// </summary>
    public static SeverityProfile Stage1Generator { get; } = new(ValidationSeverity.Error, ValidationSeverity.Warning);

    /// <summary>
    /// Stage 2 PreFlight / Pack-stage profile: fail-closed in both directions.
    /// Used when the binding set is meant to be complete and the natives are
    /// already harvested.
    /// </summary>
    public static SeverityProfile Stage2Strict { get; } = new(ValidationSeverity.Error, ValidationSeverity.Error);
}
