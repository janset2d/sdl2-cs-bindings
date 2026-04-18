using Build.Modules.Preflight.Models;

namespace Build.Modules.Contracts;

public interface IPreflightReporter
{
    void ReportRunStart();

    void ReportVersionConsistency(VersionConsistencyValidation validation);

    void ReportStrategyCoherence(StrategyCoherenceValidation validation);

    void ReportCoreLibraryIdentity(CoreLibraryIdentityValidation validation);

    void ReportCsprojPackContract(CsprojPackContractValidation validation);
}
