using Build.Results;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using IoPath = System.IO.Path;

namespace Build.Shared.Harvesting;

/// <summary>
/// Validates that satellite library closures conform to the hybrid-static packaging model.
/// In hybrid mode, a satellite's closure may contain only its own primary binaries, the core
/// SDL library, and system libraries; any other binary is a transitive dependency leak —
/// the static bake failed.
/// </summary>
/// <remarks>
/// G19 (release-guardrails). Returns <see cref="ValidationReport"/> with severity-tagged
/// checks: <see cref="ValidationMode.Strict"/> emits errors; <see cref="ValidationMode.Warn"/>
/// emits warnings; <see cref="ValidationMode.Off"/> emits an empty report. Core libraries
/// are exempt — they are the root dynamic library that satellites depend on.
/// </remarks>
public sealed class HybridStaticLeakValidator(IRuntimeProfile profile, string coreLibraryName, ValidationMode mode)
{
    private readonly IRuntimeProfile _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    private readonly string _coreLibraryName = !string.IsNullOrWhiteSpace(coreLibraryName)
        ? coreLibraryName
        : throw new ArgumentException("Core library name must be non-empty.", nameof(coreLibraryName));
    private readonly ValidationMode _mode = mode;

    public ValidationReport Validate(BinaryClosure closure, LibraryManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(closure);
        ArgumentNullException.ThrowIfNull(manifest);

        if (_mode == ValidationMode.Off || manifest.IsCoreLib)
        {
            return ValidationReport.Empty;
        }

        var violations = closure.Nodes
            .Where(node =>
                !_profile.IsSystemFile(IoPath.GetFileName(node.Path))
                && !string.Equals(_coreLibraryName, node.OwnerPackage, StringComparison.OrdinalIgnoreCase)
                && !closure.IsPrimaryFile(node.Path))
            .ToList();

        if (violations.Count == 0)
        {
            return ValidationReport.Empty;
        }

        var severity = _mode == ValidationMode.Strict
            ? ValidationSeverity.Error
            : ValidationSeverity.Warning;

        var checks = violations.Select(v => new ValidationCheck(
            Name: "Hybrid-static transitive leak",
            Severity: severity,
            Message: $"Transitive dep leak: {IoPath.GetFileName(v.Path)} (owner: {v.OwnerPackage}, origin: {v.OriginPackage})",
            Code: "G19")).ToList();

        return new ValidationReport(checks);
    }
}
