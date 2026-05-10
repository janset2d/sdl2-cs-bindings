using Build.Results;

namespace Build.Vcpkg;

public interface IPackageInfoProvider
{
    Task<Result<PackageInfo, PackageInfoError>> GetPackageInfoAsync(string packageName, string triplet, CancellationToken ct = default);
}
