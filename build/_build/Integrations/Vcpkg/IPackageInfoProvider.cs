using Build.Shared.Harvesting;

namespace Build.Integrations.Vcpkg;

public interface IPackageInfoProvider
{
    Task<PackageInfoResult> GetPackageInfoAsync(string packageName, string triplet, CancellationToken ct = default);
}
