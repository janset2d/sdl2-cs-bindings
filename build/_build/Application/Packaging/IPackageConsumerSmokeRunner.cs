using Build.Context;
using Build.Domain.Packaging.Models;

namespace Build.Application.Packaging;

/// <summary>
/// Package-consumer smoke runner contract. Single method, single shape — the request
/// record carries RID + version mapping + feed path (ADR-003 §3.2). Stateless-callable
/// so each matrix runner in <c>release.yml consumer-smoke</c> constructs its own request
/// and invokes this contract (ADR-003 §3.4 consumer-smoke matrix re-entry).
/// </summary>
public interface IPackageConsumerSmokeRunner
{
    Task RunAsync(BuildContext context, PackageConsumerSmokeRequest request, CancellationToken cancellationToken = default);
}
