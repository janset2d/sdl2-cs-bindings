using Build.Host;

namespace Build.Features.Packaging;

/// <summary>
/// Package-consumer smoke runner contract.
/// The request carries the RID, version mapping, and feed path so each workflow matrix
/// entry can invoke the same runner without relying on DI-scoped version state.
/// </summary>
public interface IPackageConsumerSmokePipeline
{
    Task RunAsync(BuildContext context, PackageConsumerSmokeRequest request, CancellationToken cancellationToken = default);
}
