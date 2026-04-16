using Build.Modules.Coverage.Models;
using Build.Modules.Coverage.Results;

namespace Build.Modules.Contracts;

public interface ICoverageThresholdValidator
{
    CoverageCheckResult Validate(CoverageMetrics metrics, CoverageBaseline baseline);
}