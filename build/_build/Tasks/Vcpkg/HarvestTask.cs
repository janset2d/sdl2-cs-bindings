using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Build.Context;
using Build.Context.Models;
using Build.Modules.DependencyAnalysis;
using Build.Modules.DependencyAnalysis.Models;
using Build.Modules.Vcpkg.Models;
using Build.Tools.Dumpbin;
using Build.Tools.Vcpkg;
using Build.Tools.Vcpkg.Settings;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using Spectre.Console;
using Cake.Core.Diagnostics;

namespace Build.Tasks.Vcpkg;

[TaskName("Harvest")]
public class HarvestTask : AsyncFrostingTask<BuildContext>
{
    private readonly RuntimeConfig _runtimeConfig;
    private readonly ManifestConfig _manifestConfig;
    private readonly SystemArtefactsConfig _systemArtefactsConfig;

    // Tracks what we've already processed to avoid duplicates and infinite recursion
    private readonly HashSet<string> _processedDlls = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _processedPackages = new(StringComparer.OrdinalIgnoreCase);

    // Collect all dependencies here
    private readonly Dictionary<string, DependencyInfo> _collectedDependencies = new(StringComparer.OrdinalIgnoreCase);
    private string? _coreLibPlatformName;

    public HarvestTask(RuntimeConfig runtimeConfig, ManifestConfig manifestConfig, SystemArtefactsConfig systemArtefactsConfig)
    {
        _runtimeConfig = runtimeConfig;
        _manifestConfig = manifestConfig;
        _systemArtefactsConfig = systemArtefactsConfig;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var libraries = context.Vcpkg.Libraries;
        var rid = context.Vcpkg.Rid ?? context.Environment.Platform.Rid();

        var runtimeInfo = _runtimeConfig.Runtimes.SingleOrDefault(info => string.Equals(info.Rid, rid, StringComparison.Ordinal)) ?? throw new CakeException($"No runtime configuration found for RID: {rid}");

        // Determine and store the core library's platform-specific name
        var coreLibManifest = _manifestConfig.LibraryManifests.FirstOrDefault(lm => lm.CoreLib);
        if (coreLibManifest != null)
        {
            _coreLibPlatformName = GetLibNameForPlatform(coreLibManifest, rid);
            if (string.IsNullOrEmpty(_coreLibPlatformName))
            {
                context.Log.Warning("Core library '{0}' defined in manifest but no platform-specific name found for RID '{1}'.", coreLibManifest.Name, rid);
            }
        }
        else
        {
            context.Log.Warning("No core library (CoreLib = true) found in manifest. Special skipping logic for core library dependencies will not apply.");
        }

        var mappingByName = _manifestConfig.LibraryManifests.ToDictionary(v => v.Name, v => v, StringComparer.OrdinalIgnoreCase);
        var knownManifests = libraries.Where(mappingByName.ContainsKey).ToList();
        var unknownManifests = libraries.Except(knownManifests, StringComparer.OrdinalIgnoreCase).ToList();

        if (unknownManifests.Count > 0)
        {
            throw new CakeException($"Unmapped libraries detected: {string.Join(", ", unknownManifests)}");
        }

        if (knownManifests.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]WARNING:[/] No libraries to process for RID: {rid}");
            return;
        }

        var vcpkgInstalledDir = context.Paths.GetVcpkgInstalledDir;
        if (!context.DirectoryExists(vcpkgInstalledDir))
        {
            throw new CakeException($"Vcpkg installed directory does not exist: {vcpkgInstalledDir.FullPath}");
        }

        AnsiConsole.MarkupLine($"[cyan]Vcpkg Installed Directory:[/] {vcpkgInstalledDir.FullPath}");

        var knownLibraryManifests = knownManifests.ConvertAll(name => mappingByName[name]);

        foreach (var libraryManifest in knownLibraryManifests)
        {
            await AnsiConsole.Status()
                .StartAsync($"Processing {libraryManifest.Name}...", async ctx =>
                {
                    var libNameForPlatform = GetLibNameForPlatform(libraryManifest, rid);
                    if (string.IsNullOrEmpty(libNameForPlatform))
                    {
                        context.Log.Error("Could not find library name for {0} on {1}", libraryManifest.Name, rid);
                        return;
                    }
                    var packageKey = $"{libraryManifest.VcpkgName}:{runtimeInfo.Triplet}";
                    ctx.Status($"Getting package info for {packageKey}");

                    var vcpkgPackageInfo = context.VcpkgPackageInfo(packageKey, new VcpkgPackageInfoSettings { JsonOutput = true, Installed = true });
                    if (vcpkgPackageInfo == null)
                    {
                        context.Log.Error("Failed to get package info for {0}", packageKey);
                        return;
                    }

                    var vcpkgInstalledPackageOutput = JsonSerializer.Deserialize<VcpkgInstalledPackageOutput>(vcpkgPackageInfo);
                    if (vcpkgInstalledPackageOutput == null)
                    {
                        context.Log.Error("Failed to deserialize package info for {0}", libraryManifest.VcpkgName);
                        return;
                    }

                    if (!vcpkgInstalledPackageOutput.Results.TryGetValue(packageKey, out var packageResult))
                    {
                        context.Log.Error("Package info does not contain {0}", packageKey);
                        return;
                    }

                    ctx.Status($"Extracting DLL path for {libraryManifest.Name}");

                    var dllPath = GetDllPathFromOwns(packageResult.Owns, libNameForPlatform, vcpkgInstalledDir);
                    if (dllPath == null)
                    {
                        context.Log.Error("Could not find DLL path for {0}", libraryManifest.Name);
                        return;
                    }

                    _collectedDependencies[libNameForPlatform] = new DependencyInfo
                    {
                        Path = dllPath.FullPath,
                        Package = libraryManifest.VcpkgName,
                    };
                    _collectedDependencies[libNameForPlatform].Sources.Add("primary");

                    var licensePath = GetLicensePathFromOwns(packageResult.Owns, vcpkgInstalledDir);
                    AnsiConsole.MarkupLine(licensePath != null ? $"License file found: {licensePath}" : $"[yellow]WARNING:[/] No license file found for {libraryManifest.Name}");

                    ctx.Status($"Collecting dependencies for {libraryManifest.Name}");

                    await Task.Run(() =>
                    {
                        CollectRuntimeDependencies(context, dllPath, libraryManifest.CoreLib);
                        CollectPackageMetadataDependencies(context, packageResult, libraryManifest.CoreLib);
                    });

                    DisplayCollectedDependencies(libraryManifest);
                });
        }
    }

    private void DisplayCollectedDependencies(LibraryManifest libraryManifest)
    {
        AnsiConsole.MarkupLine($"[green]Dependencies for {libraryManifest.Name}:[/]");

        var table = new Table();
        table.AddColumn("DLL");
        table.AddColumn("Package");
        table.AddColumn("Sources");

        foreach (var dep in _collectedDependencies)
        {
            table.AddRow(dep.Key, dep.Value.Package, string.Join(", ", dep.Value.Sources));
        }

        AnsiConsole.Write(table);
    }

    private static FilePath? GetDllPathFromOwns(IImmutableList<string> owns, string dllName, DirectoryPath basePath)
    {
        var dllRelativePath = owns
            .FirstOrDefault(path => path.EndsWith(dllName, StringComparison.OrdinalIgnoreCase) &&
                                    path.Contains("/bin/", StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrEmpty(dllRelativePath) ? null : basePath.CombineWithFilePath(dllRelativePath);
    }

    private static FilePath? GetLicensePathFromOwns(IImmutableList<string> owns, DirectoryPath basePath)
    {
        var licenseRelativePath = owns
            .FirstOrDefault(path => path.Contains("/share/", StringComparison.OrdinalIgnoreCase) &&
                                    path.EndsWith("copyright", StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrEmpty(licenseRelativePath) ? null : basePath.CombineWithFilePath(licenseRelativePath);
    }

    private static string? GetLibNameForPlatform(LibraryManifest libraryManifest, string rid)
    {
        string osFamily;
        if (rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
        {
            osFamily = "Windows";
        }
        else if (rid.StartsWith("osx-", StringComparison.OrdinalIgnoreCase))
        {
            osFamily = "OSX";
        }
        else if (rid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase))
        {
            osFamily = "Linux";
        }
        else
        {
            return null;
        }

        return libraryManifest.LibNames
            .FirstOrDefault(ln => string.Equals(ln.Os, osFamily, StringComparison.OrdinalIgnoreCase))
            ?.Name;
    }

    private void CollectRuntimeDependencies(BuildContext context, FilePath dllPath, bool isProcessingCoreLib)
    {
        var dllName = dllPath.GetFilename().FullPath;
        if (!_processedDlls.Add(dllName))
        {
            return;
        }

        var dumpbinSettings = new DumpbinDependentsSettings(dllPath.FullPath)
        {
            SetupProcessSettings = settings =>
            {
                settings.RedirectStandardOutput = true;
                settings.RedirectStandardError = true;
            },
        };

        var rawOutput = context.DumpbinDependents(dumpbinSettings);
        if (rawOutput == null)
        {
            context.Log.Warning("No dumpbin output for {0}", dllPath);
            return;
        }

        var dependentDlls = DumpbinParser.ExtractDependentDlls(rawOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

        foreach (var depDllName in dependentDlls)
        {
            if (IsSystemDll(depDllName))
            {
                continue;
            }

            // Skip core lib if we're processing a satellite library
            if (!string.IsNullOrEmpty(_coreLibPlatformName) &&
                !isProcessingCoreLib &&
                depDllName.Equals(_coreLibPlatformName, StringComparison.OrdinalIgnoreCase))
            {
                context.Log.Verbose("Skipping runtime dependency collection for '{0}' as it is the core library and we are processing a satellite library.", depDllName);
                continue;
            }

            var vcpkgBinDir = dllPath.GetDirectory();
            var depDllPath = vcpkgBinDir.CombineWithFilePath(depDllName);

            if (!context.FileExists(depDllPath))
            {
                context.Log.Warning("Dependency {0} not found at {1}", depDllName, depDllPath);
                continue;
            }

            // Add to our collection or update the sources
            if (_collectedDependencies.TryGetValue(depDllName, out var existing))
            {
                existing.Sources.Add("dumpbin");
            }
            else
            {
                _collectedDependencies[depDllName] = new DependencyInfo
                {
                    Path = depDllPath.FullPath,
                    Package = "Unknown",
                    Sources = new HashSet<string>(StringComparer.Ordinal) { "dumpbin" },
                };

                // Recursively collect dependencies of this dependency
                CollectRuntimeDependencies(context, depDllPath, isProcessingCoreLib: false);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
    private void CollectPackageMetadataDependencies(BuildContext context, VcpkgInstalledResult packageResult, bool isProcessingCoreLib)
    {
        foreach (var dependency in packageResult.Dependencies)
        {
            // Skip vcpkg tooling dependencies
            if (dependency.StartsWith("vcpkg-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Extract the package name from "packagename:triplet"
            var packageName = dependency.Split(':')[0];

            if (!_processedPackages.Add(packageName))
            {
                continue;
            }

            // Get package info
            var depPackageInfo = context.VcpkgPackageInfo(
                dependency,
                new VcpkgPackageInfoSettings { JsonOutput = true, Installed = true });

            if (depPackageInfo == null)
            {
                context.Log.Warning("Failed to get package info for dependency {0}", dependency);
                continue;
            }

            var depPackageOutput = JsonSerializer.Deserialize<VcpkgInstalledPackageOutput>(depPackageInfo);
            if (depPackageOutput == null || !depPackageOutput.Results.TryGetValue(dependency, out var depResult))
            {
                context.Log.Warning("Failed to deserialize package info for dependency {0}", dependency);
                continue;
            }

            // Find DLLs owned by this package
            foreach (var owned in depResult.Owns)
            {
                if (!owned.Contains("/bin/", StringComparison.OrdinalIgnoreCase) || !owned.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var dllName = owned[(owned.LastIndexOf('/') + 1)..];

                if (IsSystemDll(dllName))
                {
                    continue;
                }

                // Skip core lib if we're processing a satellite library
                if (!string.IsNullOrEmpty(_coreLibPlatformName) &&
                    !isProcessingCoreLib &&
                    dllName.Equals(_coreLibPlatformName, StringComparison.OrdinalIgnoreCase))
                {
                    context.Log.Verbose("Skipping metadata dependency collection for '{0}' as it is the core library and we are processing a satellite library.", dllName);
                    continue;
                }

                var vcpkgInstalledDir = context.Paths.GetVcpkgInstalledDir;
                var depDllPath = vcpkgInstalledDir.CombineWithFilePath(owned);

                if (!context.FileExists(depDllPath))
                {
                    context.Log.Warning("Dependency DLL {0} not found at {1}", dllName, depDllPath);
                    continue;
                }

                // Add to our collection or update the sources
                if (_collectedDependencies.TryGetValue(dllName, out var existing))
                {
                    existing.Sources.Add("metadata");
                    string updatedPackageName = existing.Package;

                    if (string.Equals(existing.Package, "Unknown", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(existing.Package))
                    {
                        updatedPackageName = packageName; // Update if existing was "Unknown"
                    }
                    else if (!string.Equals(existing.Package, packageName, StringComparison.OrdinalIgnoreCase))
                    {
                        // The DLL was already attributed to a different package by a previous metadata scan.
                        // This might indicate the DLL is part of multiple vcpkg packages or a complex dependency.
                        // For now, we'll log this and keep the first non-"Unknown" package attribution.
                        context.Log.Verbose(
                            "DLL '{0}' previously attributed to package '{1}'. " +
                            "Current metadata scan for package '{2}' also claims ownership. " +
                            "Keeping first attribution: '{1}'.",
                            dllName, existing.Package, packageName);
                    }

                    _collectedDependencies[dllName] = existing with
                    {
                        Path = depDllPath.FullPath, // Ensure we use the path from the metadata scan
                        Package = updatedPackageName
                    };
                }
                else
                {
                    _collectedDependencies[dllName] = new DependencyInfo
                    {
                        Path = depDllPath.FullPath,
                        Package = packageName,
                    };
                    _collectedDependencies[dllName].Sources.Add("metadata");

                    CollectRuntimeDependencies(context, depDllPath, isProcessingCoreLib: false);
                }
            }

            CollectPackageMetadataDependencies(context, depResult, isProcessingCoreLib: false);
        }
    }

    private bool IsSystemDll(string dllName)
    {
        foreach (var systemDll in _systemArtefactsConfig.Windows.SystemDlls)
        {
            if (systemDll.Contains('*', StringComparison.Ordinal))
            {
                var pattern = $"^{Regex.Escape(systemDll).Replace("\\*", @"[a-zA-Z0-9\-_\.]+", StringComparison.OrdinalIgnoreCase)}$";
                if (Regex.IsMatch(dllName, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                {
                    return true;
                }
            }
            else if (string.Equals(dllName, systemDll, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
