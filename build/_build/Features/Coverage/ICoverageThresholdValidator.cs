namespace Build.Features.Coverage;

public interface ICoverageThresholdValidator
{
    CoverageCheckResult Validate(CoverageMetrics metrics, CoverageBaseline baseline);
}
