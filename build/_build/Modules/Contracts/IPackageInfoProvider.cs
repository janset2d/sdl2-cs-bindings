using Build.Modules.Harvesting.Models;

namespace Build.Modules.Contracts;

public interface IPackageInfoProvider
{
    Task<PackageInfo?> GetPackageInfoAsync(string packageName, string triplet, CancellationToken ct = default);
}
