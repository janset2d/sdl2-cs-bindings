using Build.Host;

namespace Build.Features.Packaging;

/// <summary>
/// Pack-stage runner contract.
/// The request carries the resolved per-family version mapping explicitly; neither
/// <c>PackageTask</c> nor <c>SetupLocalDevFlow</c> reads version state from DI.
/// </summary>
public interface IPackagePipeline
{
    Task RunAsync(BuildContext context, PackRequest request, CancellationToken cancellationToken = default);
}
