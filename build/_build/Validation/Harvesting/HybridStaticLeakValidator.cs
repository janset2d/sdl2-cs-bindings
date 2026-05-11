using Build.Data.Manifest.Models;
using Build.Host.Runtime;
using Build.Results;
using Build.Targets.Harvest.Models;
using Cake.Core.IO;

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
    ValidationReport Validate(
        BinaryClosure closure,
        LibraryManifest manifest,
        string coreLibraryName,
        ValidationMode mode);
}

/// <inheritdoc />
public sealed class HybridStaticLeakValidator(IRuntimeProfile profile) : IHybridStaticLeakValidator
{
    private readonly IRuntimeProfile _profile = profile ?? throw new ArgumentNullException(nameof(profile));

    public ValidationReport Validate(BinaryClosure closure, LibraryManifest manifest, string coreLibraryName, ValidationMode mode)
    {
        ArgumentNullException.ThrowIfNull(closure);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(coreLibraryName);

        if (mode == ValidationMode.Off || manifest.IsCoreLib)
        {
            return ValidationReport.Empty;
        }

        var violations = closure.Nodes
            .Where(node =>
                !_profile.IsSystemFile(GetFilenameSegment(node.Path))
                && !string.Equals(coreLibraryName, node.OwnerPackage, StringComparison.OrdinalIgnoreCase)
                && !closure.IsPrimaryFile(node.Path))
            .ToList();

        if (violations.Count == 0)
        {
            return ValidationReport.Empty;
        }

        var severity = mode == ValidationMode.Strict
            ? ValidationSeverity.Error
            : ValidationSeverity.Warning;

        var checks = violations.Select(v => new ValidationCheck(
            Name: "Hybrid-static transitive leak",
            Severity: severity,
            Message: $"Transitive dep leak: {GetFilenameSegment(v.Path)} (owner: {v.OwnerPackage}, origin: {v.OriginPackage})",
            Code: "G19")).ToList();

        return new ValidationReport(checks);
    }

    /// <summary>
    /// Cake-native filename extraction. Wraps the raw closure node path into a Cake
    /// <see cref="FilePath"/> and returns its filename segment as a string so dependency
    /// checks use the same path semantics as the rest of the build host.
    /// </summary>
    private static string GetFilenameSegment(string nodePath) => new FilePath(nodePath).GetFilename().FullPath;
}
