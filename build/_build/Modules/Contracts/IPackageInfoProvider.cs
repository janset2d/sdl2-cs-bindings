using Build.Modules.Harvesting.Results;

namespace Build.Modules.Contracts;

public interface IPackageInfoProvider
{
    Task<PackageInfoResult> GetPackageInfoAsync(string packageName, string triplet, CancellationToken ct = default);
}
