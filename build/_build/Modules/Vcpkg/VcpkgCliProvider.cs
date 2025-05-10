namespace Build.Modules.Vcpkg;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Build.Context; // For PathService
using Build.Modules.Vcpkg.Models;
using Build.Tools.Vcpkg;
using Build.Tools.Vcpkg.Settings;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

public sealed class VcpkgCliProvider : IPackageInfoProvider
{
    private readonly ICakeContext _context;
    private readonly DirectoryPath _vcpkgInstalledDir; // Base for resolving relative paths from vcpkg output
    private readonly ICakeLog _log;

    public VcpkgCliProvider(ICakeContext context, PathService pathService, ICakeLog log)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _vcpkgInstalledDir = pathService.GetVcpkgInstalledDir;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<PackageInfo?> GetPackageInfoAsync(string packageName, string triplet, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageName);
        ArgumentException.ThrowIfNullOrEmpty(triplet);

        var packageKey = $"{packageName}:{triplet}";
        var settings = new VcpkgPackageInfoSettings { JsonOutput = true, Installed = true };

        string? vcpkgJsonOutput = await Task.Run(() => _context.VcpkgPackageInfo(packageKey, settings), ct).ConfigureAwait(false);

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
                .Select(relativeChildPath => _vcpkgInstalledDir.CombineWithFilePath(relativeChildPath))
                .ToImmutableList();

            return new PackageInfo(
                PortName: packageName,
                Triplet: triplet,
                OwnedFiles: ownedFiles,
                DeclaredDependencies: packageResult.Dependencies
            );
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to deserialize vcpkg x-package-info output for {0}. Json: {1}", packageKey, vcpkgJsonOutput);
            return null;
        }
    }

    public async Task<PackageInfo?> GetPackageInfoForFileAsync(FilePath installedFilePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(installedFilePath);

        _log.Warning("{0} is not fully implemented yet due to complexity of 'vcpkg owns' path requirements and reliable package name extraction from an arbitrary file path.", nameof(GetPackageInfoForFileAsync));

        // Basic heuristic: try to infer from path segments like: .../vcpkg_installed/<triplet>/<type>/<package-name>/...
        // This is very fragile and should be replaced with a robust `vcpkg owns` call if possible.
        try
        {
            var segments = installedFilePath.Segments;
            // Example path: .../vcpkg_installed/x64-windows-release/share/sdl2/copyright
            // We need to find the triplet and then the package name after it, usually separated by share/bin/lib/include

            int vcpkgInstalledIndex = -1;
            for(int i=0; i < segments.Length; ++i)
            {
                if(segments[i].Equals("vcpkg_installed", StringComparison.OrdinalIgnoreCase))
                {
                    vcpkgInstalledIndex = i;
                    break;
                }
            }

            if (vcpkgInstalledIndex != -1 && vcpkgInstalledIndex + 3 < segments.Length)
            {
                // Segment after vcpkg_installed should be the triplet
                string inferredTriplet = segments[vcpkgInstalledIndex + 1];
                // Segment after triplet should be type (share, bin, lib, include)
                string typeSegment = segments[vcpkgInstalledIndex + 2];
                // Segment after type should be package name
                string inferredPackageName = segments[vcpkgInstalledIndex + 3];

                if (!string.IsNullOrWhiteSpace(inferredTriplet) &&
                    !string.IsNullOrWhiteSpace(inferredPackageName) &&
                    (typeSegment.Equals("share", StringComparison.OrdinalIgnoreCase) ||
                     typeSegment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                     typeSegment.Equals("lib", StringComparison.OrdinalIgnoreCase) ||
                     typeSegment.Equals("include", StringComparison.OrdinalIgnoreCase)))
                {
                    _log.Debug("Attempting to infer package from path: Name='{0}', Triplet='{1}' for file '{2}'", inferredPackageName, inferredTriplet, installedFilePath);
                    return await GetPackageInfoAsync(inferredPackageName, inferredTriplet, ct).ConfigureAwait(false);
                }
            }
        }
        catch(Exception ex) // General catch for heuristic; CA1031 acknowledged for manual review
        {
             _log.Warning(ex, "Heuristic package name inference failed for {0}", installedFilePath.FullPath);
        }

        _log.Error("Could not determine package owning file '{0}' for GetPackageInfoForFileAsync using heuristic.", installedFilePath.FullPath);
        return null; // Not implemented robustly yet
    }
}
