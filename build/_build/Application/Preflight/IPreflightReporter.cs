using Build.Domain.Preflight.Models;

namespace Build.Application.Preflight;

public interface IPreflightReporter
{
    void ReportRunStart();

    void ReportVersionConsistency(VersionConsistencyValidation validation);

    void ReportStrategyCoherence(StrategyCoherenceValidation validation);

    void ReportCoreLibraryIdentity(CoreLibraryIdentityValidation validation);

    void ReportUpstreamVersionAlignment(UpstreamVersionAlignmentValidation validation);

    void ReportCsprojPackContract(CsprojPackContractValidation validation);
}
