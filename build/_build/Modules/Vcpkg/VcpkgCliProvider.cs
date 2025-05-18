#pragma warning disable CA1031

using System.Collections.Immutable;
using System.Text.Json;
using Build.Modules.Vcpkg.Models;
using Build.Tools.Vcpkg;
using Build.Tools.Vcpkg.Settings;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Modules.Vcpkg;

public sealed class VcpkgCliProvider : IPackageInfoProvider
{
    private readonly ICakeContext _context;
    private readonly DirectoryPath _vcpkgRoot;
    private readonly DirectoryPath _vcpkgInstallDir;
    private readonly ICakeLog _log;

    public VcpkgCliProvider(ICakeContext context, PathService pathService, ICakeLog log)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _vcpkgRoot = pathService.VcpkgRoot;
        _vcpkgInstallDir = pathService.GetVcpkgInstalledDir;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<PackageInfo?> GetPackageInfoAsync(string packageName, string triplet, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageName);
        ArgumentException.ThrowIfNullOrEmpty(triplet);

        var packageKey = $"{packageName}:{triplet}";
        var settings = new VcpkgPackageInfoSettings(_vcpkgRoot) { JsonOutput = true, Installed = true };

        var vcpkgJsonOutput = await Task.Run(() => _context.VcpkgPackageInfo(packageKey, settings), ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(vcpkgJsonOutput))
        {
            _log.Warning("Vcpkg x-package-info returned no output for {0}.", packageKey);
            return null;
        }

        try
        {
            var vcpkgInstalledOutput = JsonSerializer.Deserialize<VcpkgInstalledPackageOutput>(vcpkgJsonOutput);
            if (vcpkgInstalledOutput == null || !vcpkgInstalledOutput.Results.TryGetValue(packageKey, out var packageResult))
            {
                _log.Warning("Failed to deserialize or find package info for {0} in vcpkg output.", packageKey);
                return null;
            }

            var ownedFiles = packageResult.Owns
                .Select(relativeChildPath => _vcpkgInstallDir.CombineWithFilePath(relativeChildPath))
                .ToImmutableList();

            return new PackageInfo(
                PackageName: packageName,
                Triplet: triplet,
                OwnedFiles: ownedFiles,
                DeclaredDependencies: packageResult.Dependencies
            );
        }
        catch (JsonException ex)
        {
            _log.Error("Failed to deserialize vcpkg x-package-info output for {0}. Json: {1}. Message {3}", packageKey, vcpkgJsonOutput, ex.Message);
            return null;
        }
    }
}
