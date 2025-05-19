#pragma warning disable CA1031

using System.Collections.Immutable;
using System.Text.Json;
using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Build.Tools.Vcpkg;
using Build.Tools.Vcpkg.Settings;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Modules.Harvesting;

public sealed class VcpkgCliProvider : IPackageInfoProvider
{
    private readonly ICakeContext _context;
    private readonly DirectoryPath _vcpkgRoot;
    private readonly DirectoryPath _vcpkgInstallDir;
    private readonly ICakeLog _log;

    public VcpkgCliProvider(ICakeContext context, IPathService pathService, ICakeLog log)
    {
        ArgumentNullException.ThrowIfNull(pathService);

        _context = context ?? throw new ArgumentNullException(nameof(context));
        _vcpkgRoot = pathService.VcpkgRoot;
        _vcpkgInstallDir = pathService.GetVcpkgInstalledDir;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<PackageInfoResult> GetPackageInfoAsync(string packageName, string triplet, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageName);
        ArgumentException.ThrowIfNullOrEmpty(triplet);

        try
        {
            var packageKey = $"{packageName}:{triplet}";
            var settings = new VcpkgPackageInfoSettings(_vcpkgRoot) { JsonOutput = true, Installed = true };

            var vcpkgJsonOutput = await Task.Run(() => _context.VcpkgPackageInfo(packageKey, settings), ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(vcpkgJsonOutput))
            {
                var message = $"Vcpkg x-package-info returned no output for {packageKey}.";
                _log.Warning(message);
                return new PackageInfoError(message);
            }

            var vcpkgInstalledOutput = JsonSerializer.Deserialize<VcpkgInstalledPackageOutput>(vcpkgJsonOutput);
            if (vcpkgInstalledOutput == null || !vcpkgInstalledOutput.Results.TryGetValue(packageKey, out var packageResult))
            {
                var message = $"Failed to deserialize or find package info for {packageKey} in vcpkg output.";
                _log.Warning(message);
                return new PackageInfoError(message);
            }

            var ownedFiles = packageResult.Owns
                .Select(relativeChildPath => _vcpkgInstallDir.CombineWithFilePath(relativeChildPath))
                .ToImmutableList();

            return new PackageInfo(PackageName: packageName, Triplet: triplet, OwnedFiles: ownedFiles, DeclaredDependencies: packageResult.Dependencies);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new PackageInfoError($"Error building dependency closure: {ex.Message}", ex);
        }
    }
}
