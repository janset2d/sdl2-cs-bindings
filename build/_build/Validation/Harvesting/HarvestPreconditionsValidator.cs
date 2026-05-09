using Build.Host.Paths;
using Build.Results;
using Build.Shared.Runtime;
using Cake.Common.IO;
using Cake.Core;

namespace Build.Validation.Harvesting;

public sealed class HarvestPreconditionsValidator(
    ICakeContext cakeContext,
    IPathService pathService,
    IRuntimeProfile runtimeProfile) : IHarvestPreconditionsValidator
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));

    public ValidationReport Validate()
    {
        var checks = new List<ValidationCheck>();
        var tripletDir = _pathService.GetVcpkgInstalledTripletDir(_runtimeProfile.Triplet);

        if (!_cakeContext.DirectoryExists(tripletDir))
        {
            checks.Add(new ValidationCheck(
                Name: "Harvest vcpkg triplet directory",
                Severity: ValidationSeverity.Error,
                Message: $"Harvest precondition failed: vcpkg triplet directory '{tripletDir.FullPath}' is missing for triplet '{_runtimeProfile.Triplet}'. " +
                         $"Run '--target EnsureVcpkgDependencies --rid {_runtimeProfile.Rid}' first."));
        }

        return checks.Count == 0 ? ValidationReport.Empty : new ValidationReport(checks);
    }
}
