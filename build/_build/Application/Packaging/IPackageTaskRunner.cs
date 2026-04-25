using Build.Context;
using Build.Domain.Packaging.Models;

namespace Build.Application.Packaging;

/// <summary>
/// Pack-stage runner contract.
/// The request carries the resolved per-family version mapping explicitly; neither
/// <c>PackageTask</c> nor <c>SetupLocalDevTaskRunner</c> reads version state from DI.
/// </summary>
public interface IPackageTaskRunner
{
    Task RunAsync(BuildContext context, PackRequest request, CancellationToken cancellationToken = default);
}
