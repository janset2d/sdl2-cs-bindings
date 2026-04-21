using Build.Context;
using Build.Domain.Packaging.Models;

namespace Build.Application.Packaging;

/// <summary>
/// Pack-stage runner contract. Single method, single shape — the request record carries
/// the resolved per-family version mapping (ADR-003 §3.2). Both <c>PackageTask</c> and
/// <c>SetupLocalDevTaskRunner</c> construct the request explicitly; neither path reads
/// mapping state from DI.
/// </summary>
public interface IPackageTaskRunner
{
    Task RunAsync(BuildContext context, PackRequest request, CancellationToken cancellationToken = default);
}
