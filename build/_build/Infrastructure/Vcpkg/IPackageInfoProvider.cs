using Build.Domain.Harvesting.Results;

namespace Build.Infrastructure.Vcpkg;

public interface IPackageInfoProvider
{
    Task<PackageInfoResult> GetPackageInfoAsync(string packageName, string triplet, CancellationToken ct = default);
}
