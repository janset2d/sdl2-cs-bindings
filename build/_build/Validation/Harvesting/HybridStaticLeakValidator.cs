using Build.Data.Manifest.Models;
using Build.Host.Runtime;
using Build.Results;
using Build.Targets.Harvest.Models;
using Cake.Core.IO;

namespace Build.Validation.Harvesting;

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

        var checks = violations
            .Select(v => new ValidationCheck(
                Name: "Hybrid-static transitive leak",
                Severity: severity,
                Message: $"Transitive dep leak: {GetFilenameSegment(v.Path)} (owner: {v.OwnerPackage}, origin: {v.OriginPackage})",
                Code: "G19"))
            .ToList();

        return new ValidationReport(checks);
    }

    /// <summary>
    /// Cake-native filename extraction. Wraps the raw closure node path into a Cake
    /// <see cref="FilePath"/> and returns its filename segment as a string so dependency
    /// checks use the same path semantics as the rest of the build host.
    /// </summary>
    private static string GetFilenameSegment(string nodePath) => new FilePath(nodePath).GetFilename().FullPath;
}
