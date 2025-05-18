namespace Build.Modules.Vcpkg;

using System.Threading;
using System.Threading.Tasks;
using Models; // For PackageInfo

// For FilePath

public interface IPackageInfoProvider
{
    // Gets info based on a package name and triplet
    Task<PackageInfo?> GetPackageInfoAsync(string packageName, string triplet, CancellationToken ct = default);

    // Gets info based on a file path known to be within a package's installed directory
    // This might be more complex to implement reliably with vcpkg CLI if `vcpkg owns` is slow or not granular enough.
    // For now, we can stub it or think about its best implementation.
    // Your friend's VcpkgHarvesterService implied its existence: `var pkgInfo = await _pkg.GetInfoAsync(bin, ct);`
    // Let's call it GetPackageInfoForFileAsync for clarity that it takes a FilePath.
    // Task<PackageInfo?> GetPackageInfoForFileAsync(FilePath installedFilePath, CancellationToken ct = default);
}
