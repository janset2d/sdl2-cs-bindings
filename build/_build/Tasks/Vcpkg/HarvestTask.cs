using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Build.Context;
using Build.Modules;
using Build.Modules.DependencyAnalysis;
using Build.Tools.Dumpbin;
using Build.Tools.Vcpkg;
using Build.Tools.Vcpkg.Settings;
using Cake.Common.IO;
using Cake.Core.IO;
using Cake.Frosting;
using Spectre.Console;

namespace Build.Tasks.Vcpkg;

[TaskName("Harvest")]
public class HarvestTask : AsyncFrostingTask<BuildContext>
{
    // List of system DLLs to exclude
    private static readonly HashSet<string> SystemDlls = new(StringComparer.OrdinalIgnoreCase)
    {
        "kernel32.dll",
        "user32.dll",
        "gdi32.dll",
        "winmm.dll",
        "imm32.dll",
        "version.dll",
        "setupapi.dll",
        "winspool.dll",
        "comdlg32.dll",
        "advapi32.dll",
        "shell32.dll",
        "ole32.dll",
        "oleaut32.dll",
        "uuid.dll",
        "odbc32.dll",
        "odbccp32.dll",
        "comctl32.dll",
        "ws2_32.dll",
        "d3d9.dll",
        "d3d11.dll",
        "dxgi.dll",
        "shlwapi.dll",
        "crypt32.dll",
        "msvcp140.dll",
        "vcruntime140.dll",
        "vcruntime140_1.dll",
        "api-ms-win-*.dll",
    };

    // Tracks what we've already processed to avoid duplicates and infinite recursion
    private readonly HashSet<string> _processedDlls = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _processedPackages = new(StringComparer.OrdinalIgnoreCase);

    // Collect all dependencies here
    private readonly Dictionary<string, DependencyInfo> _collectedDependencies = new(StringComparer.OrdinalIgnoreCase);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var runtimesFile = context.Paths.GetRuntimesFile();
        var manifestFile = context.Paths.GetManifestFile();
        var libraries = context.Vcpkg.Libraries;
        var rid = context.Vcpkg.Rid ?? context.Environment.Platform.Rid();

        var runtimeConfig = await context.ToJsonAsync<RuntimeConfig>(runtimesFile);
        var manifestConfig = await context.ToJsonAsync<ManifestConfig>(manifestFile);

        var runtimeInfo = runtimeConfig.Runtimes.SingleOrDefault(info => string.Equals(info.Rid, rid, StringComparison.Ordinal));
        if (runtimeInfo == null)
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] No runtime configuration found for RID: {rid}");
            Environment.Exit(1);
            return;
        }

        var mappingByName = manifestConfig.LibraryManifests.ToDictionary(v => v.Name, v => v, StringComparer.OrdinalIgnoreCase);
        var knownLibs = libraries.Where(mappingByName.ContainsKey).ToList();
        var unknownLibs = libraries.Except(knownLibs, StringComparer.OrdinalIgnoreCase).ToList();

        if (unknownLibs.Count > 0)
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] Unmapped libraries detected: {string.Join(", ", unknownLibs)}");
            Environment.Exit(1);
            return;
        }

        if (knownLibs.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]WARNING:[/] No libraries to process for RID: {rid}");
            return;
        }

        var vcpkgInstalledDir = context.Paths.GetVcpkgInstalledDir;
        if (!context.DirectoryExists(vcpkgInstalledDir))
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] Vcpkg installed directory does not exist: {vcpkgInstalledDir.FullPath}");
            Environment.Exit(1);
            return;
        }

        AnsiConsole.MarkupLine($"[cyan]Vcpkg Installed Directory:[/] {vcpkgInstalledDir.FullPath}");

        var knownVersionInfos = knownLibs.ConvertAll(name => mappingByName[name]);

        foreach (var versionInfo in knownVersionInfos)
        {
            await AnsiConsole.Status()
                .StartAsync($"Processing {versionInfo.Name}...", async ctx =>
                {
                    var libNameForPlatform = GetLibNameForPlatform(versionInfo, rid);
                    if (string.IsNullOrEmpty(libNameForPlatform))
                    {
                        AnsiConsole.MarkupLine($"[red]ERROR:[/] Could not find library name for {versionInfo.Name} on {rid}");
                        return;
                    }
                    var packageKey = $"{versionInfo.VcpkgName}:{runtimeInfo.Triplet}";
                    ctx.Status($"Getting package info for {packageKey}");

                    var vcpkgPackageInfo = context.VcpkgPackageInfo(packageKey, new VcpkgPackageInfoSettings { JsonOutput = true, Installed = true });
                    if (vcpkgPackageInfo == null)
                    {
                        AnsiConsole.MarkupLine($"[red]ERROR:[/] Failed to get package info for {packageKey}");
                        return;
                    }

                    var vcpkgInstalledPackageOutput = JsonSerializer.Deserialize<VcpkgInstalledPackageOutput>(vcpkgPackageInfo);
                    if (vcpkgInstalledPackageOutput == null)
                    {
                        AnsiConsole.MarkupLine($"[red]ERROR:[/] Failed to deserialize package info for {versionInfo.VcpkgName}");
                        return;
                    }

                    if (!vcpkgInstalledPackageOutput.Results.TryGetValue(packageKey, out var packageResult))
                    {
                        AnsiConsole.MarkupLine($"[red]ERROR:[/] Package info does not contain {packageKey}");
                        return;
                    }

                    ctx.Status($"Extracting DLL path for {versionInfo.Name}");

                    var dllPath = GetDllPathFromOwns(packageResult.Owns, libNameForPlatform, vcpkgInstalledDir);
                    if (dllPath == null)
                    {
                        AnsiConsole.MarkupLine($"[red]ERROR:[/] Could not find DLL path for {versionInfo.Name}");
                        return;
                    }

                    _collectedDependencies[libNameForPlatform] = new DependencyInfo
                    {
                        Path = dllPath.FullPath,
                        Package = versionInfo.VcpkgName,
                    };
                    _collectedDependencies[libNameForPlatform].Sources.Add("primary");

                    var licensePath = GetLicensePathFromOwns(packageResult.Owns, vcpkgInstalledDir);
                    AnsiConsole.MarkupLine(licensePath != null ? $"License file found: {licensePath}" : $"[yellow]WARNING:[/] No license file found for {versionInfo.Name}");

                    ctx.Status($"Collecting dependencies for {versionInfo.Name}");

                    await Task.Run(() =>
                    {
                        CollectRuntimeDependencies(context, dllPath, versionInfo.CoreLib);

                        CollectPackageMetadataDependencies(context, packageResult, versionInfo.CoreLib);
                    });

                    DisplayCollectedDependencies(versionInfo);
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

    private void CollectRuntimeDependencies(BuildContext context, FilePath dllPath, bool isCoreLib)
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
            AnsiConsole.MarkupLine($"[yellow]WARNING:[/] No dumpbin output for {dllPath}");
            return;
        }

        var dependentDlls = DumpbinParser.ExtractDependentDlls(rawOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

        foreach (var depDllName in dependentDlls)
        {
            if (IsSystemDll(depDllName))
            {
                continue;
            }

            // Skip SDL2.dll if we're processing a satellite library (and it's not explicitly a core lib)
            if (!isCoreLib && depDllName.Equals("SDL2.dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var vcpkgBinDir = dllPath.GetDirectory();
            var depDllPath = vcpkgBinDir.CombineWithFilePath(depDllName);

            if (!context.FileExists(depDllPath))
            {
                AnsiConsole.MarkupLine($"[yellow]WARNING:[/] Dependency {depDllName} not found at {depDllPath}");
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
                CollectRuntimeDependencies(context, depDllPath, isCoreLib: false);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
    private void CollectPackageMetadataDependencies(BuildContext context, VcpkgInstalledResult packageResult, bool isCoreLib)
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
                AnsiConsole.MarkupLine($"[yellow]WARNING:[/] Failed to get package info for dependency {dependency}");
                continue;
            }

            var depPackageOutput = JsonSerializer.Deserialize<VcpkgInstalledPackageOutput>(depPackageInfo);
            if (depPackageOutput == null || !depPackageOutput.Results.TryGetValue(dependency, out var depResult))
            {
                AnsiConsole.MarkupLine($"[yellow]WARNING:[/] Failed to deserialize package info for dependency {dependency}");
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

                // Skip SDL2.dll if we're processing a satellite library
                if (!isCoreLib && dllName.Equals("SDL2.dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var vcpkgInstalledDir = context.Paths.GetVcpkgInstalledDir;
                var depDllPath = vcpkgInstalledDir.CombineWithFilePath(owned);

                if (!context.FileExists(depDllPath))
                {
                    AnsiConsole.MarkupLine($"[yellow]WARNING:[/] Dependency DLL {dllName} not found at {depDllPath}");
                    continue;
                }

                // Add to our collection or update the sources
                if (_collectedDependencies.TryGetValue(dllName, out var existing))
                {
                    existing.Sources.Add("metadata");
                    _collectedDependencies[dllName] = existing with { Path = depDllPath.FullPath };
                }
                else
                {
                    _collectedDependencies[dllName] = new DependencyInfo
                    {
                        Path = depDllPath.FullPath,
                        Package = packageName,
                    };
                    _collectedDependencies[dllName].Sources.Add("metadata");

                    CollectRuntimeDependencies(context, depDllPath, isCoreLib: false);
                }
            }

            CollectPackageMetadataDependencies(context, depResult, isCoreLib: false);
        }
    }

    private static bool IsSystemDll(string dllName)
    {
        foreach (var systemDll in SystemDlls)
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

// Represents collected information about a dependency
public record DependencyInfo
{
    public required string Path { get; init; }

    public required string Package { get; init; }

    public ISet<string> Sources { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}

public record RuntimeInfo
{
    [JsonPropertyName("rid")] public required string Rid { get; init; }

    [JsonPropertyName("triplet")] public required string Triplet { get; init; }

    [JsonPropertyName("runner")] public required string Runner { get; init; }

    [JsonPropertyName("container_image")] public string? ContainerImage { get; init; }
}

public record RuntimeConfig
{
    [JsonPropertyName("runtimes")] public required IImmutableList<RuntimeInfo> Runtimes { get; init; }
}

public record LibraryManifest
{
    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("vcpkg_name")] public required string VcpkgName { get; init; }

    [JsonPropertyName("vcpkg_version")] public required string VcpkgVersion { get; init; }

    [JsonPropertyName("vcpkg_port_version")]
    public required int VcpkgPortVersion { get; init; }

    [JsonPropertyName("native_lib_name")] public required string NativeLibName { get; init; }

    [JsonPropertyName("native_lib_version")]
    public required string NativeLibVersion { get; init; }

    [JsonPropertyName("core_lib")] public required bool CoreLib { get; init; }

    [JsonPropertyName("lib_names")] public required IImmutableList<LibName> LibNames { get; init; }
}

public record LibName
{
    [JsonPropertyName("os")] public required string Os { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; } = string.Empty;
}

public record ManifestConfig
{
    [JsonPropertyName("library_manifests")]
    public required IImmutableList<LibraryManifest> LibraryManifests { get; init; }
}

public record VcpkgInstalledPackageOutput
{
    [JsonPropertyName("results")]
    public required ImmutableDictionary<string, VcpkgInstalledResult> Results { get; init; }
}

public record VcpkgInstalledResult
{
    [JsonPropertyName("version-string")] public required string VersionString { get; init; }

    [JsonPropertyName("port-version")] public required int PortVersion { get; init; }

    [JsonPropertyName("triplet")] public required string Triplet { get; init; }

    [JsonPropertyName("abi")] public string? Abi { get; init; }

    [JsonPropertyName("dependencies")] public required IImmutableList<string> Dependencies { get; init; }

    [JsonPropertyName("features")] public IImmutableList<string>? Features { get; init; }

    [JsonPropertyName("usage")] public string? Usage { get; init; }

    [JsonPropertyName("owns")] public required IImmutableList<string> Owns { get; init; }
}
