using Build.Harvesting;
using Build.Manifest;
using Build.Results;

namespace Build.Validation.Harvesting;

/// <summary>
/// Validates that satellite library closures conform to the hybrid-static packaging model.
/// G19 (release-guardrails). Returns <see cref="ValidationReport"/> with severity-tagged
/// checks; the validator's configured <see cref="ValidationMode"/> selects strict (errors),
/// warn (warnings), or off (empty report). Core libraries are exempt — they are the root
/// dynamic library that satellites depend on.
/// </summary>
public interface IHybridStaticLeakValidator
{
    ValidationReport Validate(BinaryClosure closure, LibraryManifest manifest);
}
