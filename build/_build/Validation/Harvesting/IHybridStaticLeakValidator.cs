using Build.Data.Manifest.Models;
using Build.Results;
using Build.Targets.Harvest.Models;

namespace Build.Validation.Harvesting;
/// <summary>
/// Validates that satellite library closures conform to the hybrid-static packaging model.
/// In hybrid mode, a satellite's closure may contain only its own primary binaries, the core
/// SDL library, and system libraries; any other binary is a transitive dependency leak:
/// the static bake failed.
/// </summary>
/// <remarks>
/// G19 (release-guardrails). Returns <see cref="ValidationReport"/> with severity-tagged
/// checks: <see cref="ValidationMode.Strict"/> emits errors; <see cref="ValidationMode.Warn"/>
/// emits warnings; <see cref="ValidationMode.Off"/> emits an empty report. Core libraries
/// are exempt because they are the root dynamic library that satellites depend on.
/// </remarks>
public interface IHybridStaticLeakValidator
{
    public ValidationReport Validate(
        BinaryClosure closure,
        LibraryManifest manifest,
        string coreLibraryName,
        ValidationMode mode);
}
