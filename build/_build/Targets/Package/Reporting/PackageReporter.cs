using Build.Data.Manifest.Models;
using Build.Data.ProjectMetadata;
using Build.Results;
using Build.Targets.Package.Models;
using Build.Validation.Models;
using Cake.Core.Diagnostics;

namespace Build.Targets.Package.Reporting;

/// <summary>
/// Consolidates Pack-stage logging behind a single named seam. Sealed concrete; no
/// IPackageReporter interface (single consumer, ICakeLog is the only collaborator).
/// Mirrors the S12 PreflightReporter shape: Log* methods for normal progress,
/// Report* methods for expected error paths whose details ship into the log before
/// the task throws CakeException at the boundary.
/// </summary>
public sealed class PackageReporter(ICakeLog log)
{
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public void LogPackingFamily(string familyName, string version)
    {
        _log.Information("Packing family '{0}' at version '{1}'.", familyName, version);
    }

    public void LogPackedFamily(PackageFamilyConfig family, PackageArtifacts artifacts)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(artifacts);

        _log.Information(
            "Packed family '{0}': {1}, {2}, {3}",
            family.Name,
            artifacts.NativePackage.GetFilename().FullPath,
            artifacts.ManagedPackage.GetFilename().FullPath,
            artifacts.ManagedSymbolsPackage.GetFilename().FullPath);
    }

    public void ReportValidationDiagnostics(PackageFamilyConfig family, ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(report);

        foreach (var error in report.Errors)
        {
            _log.Error("  - [{0}] {1}", error.Code ?? error.Name, error.Message);
        }

        // PackageOutputValidator only adds findings to the report — passing checks aren't
        // enumerated. So a clean report logs "no validation errors" rather than a misleading
        // "0 check(s) passed" count.
        if (report.IsValid)
        {
            _log.Verbose("Family '{0}' post-pack validation: no validation errors.", family.Name);
        }
    }

    public void ReportPackError(PackageFamilyConfig family, DotNetPackError error)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(error);

        _log.Error("dotnet pack failed for family '{0}': {1}", family.Name, error.Message);
        if (error.Exception is not null)
        {
            _log.Verbose("Details: {0}", error.Exception);
        }
    }

    public void ReportProjectMetadataError(PackageFamilyConfig family, ProjectMetadataError error)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(error);

        _log.Error("Project metadata resolution failed for family '{0}': {1}", family.Name, error.Message);
    }

    public void ReportCrossFamilyResolvabilityErrors(CrossFamilyDependencyValidation validation)
    {
        ArgumentNullException.ThrowIfNull(validation);

        foreach (var check in validation.Checks.Where(check => check.IsError))
        {
            _log.Error("[G58] {0} → {1}: {2}", check.DependentFamily, check.DependencyFamily, check.ErrorMessage);
        }
    }
}
