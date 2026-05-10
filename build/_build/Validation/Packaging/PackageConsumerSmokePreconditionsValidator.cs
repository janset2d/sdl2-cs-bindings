using Build.Results;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Validation.Packaging;

/// <summary>
/// Pre-flight validator for the PackageConsumerSmoke target. Asserts the smoke csproj,
/// the compile-sanity csproj, and the local feed directory all exist before the task
/// invokes <c>dotnet build</c> / <c>dotnet test</c>. Errors carry guardrail-style codes
/// (<c>PCSP-01</c>..<c>03</c>) that reporters and logs surface alongside the message.
/// </summary>
public sealed class PackageConsumerSmokePreconditionsValidator(ICakeContext cakeContext)
    : IPackageConsumerSmokePreconditionsValidator
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));

    public ValidationReport Validate(FilePath smokeCsproj, FilePath compileSanityCsproj, DirectoryPath feedPath)
    {
        ArgumentNullException.ThrowIfNull(smokeCsproj);
        ArgumentNullException.ThrowIfNull(compileSanityCsproj);
        ArgumentNullException.ThrowIfNull(feedPath);

        var checks = new List<ValidationCheck>();

        if (!_cakeContext.FileExists(smokeCsproj))
        {
            checks.Add(new ValidationCheck(
                Name: "Smoke project exists",
                Severity: ValidationSeverity.Error,
                Message: $"PackageConsumerSmoke precondition failed: smoke project '{smokeCsproj.FullPath}' is missing. Sync the repository checkout before running the consumer smoke stage.",
                Code: "PCSP-01"));
        }

        if (!_cakeContext.FileExists(compileSanityCsproj))
        {
            checks.Add(new ValidationCheck(
                Name: "Compile-sanity project exists",
                Severity: ValidationSeverity.Error,
                Message: $"PackageConsumerSmoke precondition failed: compile-sanity project '{compileSanityCsproj.FullPath}' is missing. Sync the repository checkout before running the consumer smoke stage.",
                Code: "PCSP-02"));
        }

        if (!_cakeContext.DirectoryExists(feedPath))
        {
            checks.Add(new ValidationCheck(
                Name: "Local feed directory exists",
                Severity: ValidationSeverity.Error,
                Message: $"PackageConsumerSmoke precondition failed: local feed directory '{feedPath.FullPath}' is missing. Pack the local feed first via '--target Package --versions-file <path>' or '--target Package --explicit-version <family>=<semver>'.",
                Code: "PCSP-03"));
        }

        return new ValidationReport(checks);
    }
}
