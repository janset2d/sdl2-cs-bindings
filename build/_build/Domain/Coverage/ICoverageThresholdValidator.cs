using Build.Domain.Coverage.Models;
using Build.Domain.Coverage.Results;

namespace Build.Domain.Coverage;

public interface ICoverageThresholdValidator
{
    CoverageCheckResult Validate(CoverageMetrics metrics, CoverageBaseline baseline);
}