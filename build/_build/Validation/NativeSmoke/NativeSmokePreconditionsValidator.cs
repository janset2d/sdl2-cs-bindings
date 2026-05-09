using Build.Host.Paths;
using Build.Results;
using Cake.Common.IO;
using Cake.Core;

namespace Build.Validation.NativeSmoke;

public sealed class NativeSmokePreconditionsValidator(
    ICakeContext cakeContext,
    IPathService pathService) : INativeSmokePreconditionsValidator
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    public ValidationReport Validate()
    {
        var checks = new List<ValidationCheck>();
        var projectDir = _pathService.NativeSmokeProjectDir;

        if (!_cakeContext.DirectoryExists(projectDir))
        {
            checks.Add(new ValidationCheck(
                Name: "NativeSmoke project directory",
                Severity: ValidationSeverity.Error,
                Message: $"NativeSmoke precondition failed: project directory '{projectDir.FullPath}' is missing. " +
                         "Sync the repository checkout before running the native smoke stage."));

            return new ValidationReport(checks);
        }

        var cmakeListsFile = projectDir.CombineWithFilePath("CMakeLists.txt");
        if (!_cakeContext.FileExists(cmakeListsFile))
        {
            checks.Add(new ValidationCheck(
                Name: "NativeSmoke CMakeLists.txt",
                Severity: ValidationSeverity.Error,
                Message: $"NativeSmoke precondition failed: '{cmakeListsFile.FullPath}' is missing. " +
                         "Sync the repository checkout before running the native smoke stage."));
        }

        var cmakePresetsFile = projectDir.CombineWithFilePath("CMakePresets.json");
        if (!_cakeContext.FileExists(cmakePresetsFile))
        {
            checks.Add(new ValidationCheck(
                Name: "NativeSmoke CMakePresets.json",
                Severity: ValidationSeverity.Error,
                Message: $"NativeSmoke precondition failed: '{cmakePresetsFile.FullPath}' is missing. " +
                         "Sync the repository checkout before running the native smoke stage."));
        }

        return checks.Count == 0 ? ValidationReport.Empty : new ValidationReport(checks);
    }
}
