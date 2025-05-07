using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Build.Context;
using Build.Modules;
using Build.Tools.Vcpkg;
using Build.Tools.Vcpkg.Settings;
using Cake.Common.IO;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Spectre.Console;

namespace Build.Tasks.Vcpkg;

[TaskName("Harvest")]
public class HarvestTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        var runtimesFile = context.Paths.GetRuntimesFile();
        var versionMappingFile = context.Paths.GetVersionMappingFile();
        var libraries = context.Vcpkg.Libraries;
        var rid = context.Vcpkg.Rid ?? context.Environment.Platform.Rid();

        var runtimeConfig = await context.ToJsonAsync<RuntimeConfig>(runtimesFile);
        var versionMappingConfig = await context.ToJsonAsync<VersionMappingConfig>(versionMappingFile);

        var runtimeInfo = runtimeConfig.Runtimes.SingleOrDefault(info => string.Equals(info.Rid, rid, StringComparison.Ordinal));
        if (runtimeInfo == null)
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] No runtime configuration found for RID: {rid}");
            Environment.Exit(1);
            return;
        }

        var mappingByName = versionMappingConfig.VersionMapping.ToDictionary(v => v.Name, v => v, StringComparer.OrdinalIgnoreCase);
        var knownLibs   = libraries.Where(mappingByName.ContainsKey).ToList();
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

        var vcpkgInstalledDir = context.Paths.GetVcpkgInstalledDir(runtimeInfo.Triplet);
        if (!context.DirectoryExists(vcpkgInstalledDir))
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] Vcpkg installed directory does not exist: {vcpkgInstalledDir.FullPath}");
            Environment.Exit(1);
            return;
        }

        AnsiConsole.MarkupLine($"[cyan]Vcpkg Installed Directory:[/] {vcpkgInstalledDir.FullPath}");
        // var vcpkgInstalledBinDir = context.Paths.GetVcpkgInstalledBinDir(runtimeInfo.Triplet);
        // var filePathCollection = context.GetFiles(GlobPattern.FromString($"{vcpkgInstalledBinDir.FullPath}/**/*.dll"));
        //
        // foreach (var file in filePathCollection)
        // {
        //     AnsiConsole.MarkupLine($"[cyan]File:[/] {file.FullPath}");
        // }
        var knownVersionInfos = knownLibs.ConvertAll(name => mappingByName[name]);
#pragma warning disable S3267
        foreach (var v in knownVersionInfos)
#pragma warning restore S3267
        {
            var vcpkgPackageInfo = context.VcpkgPackageInfo($"{v.VcpkgName}:{runtimeInfo.Triplet}", new VcpkgPackageInfoSettings() {Installed = true});

            if (vcpkgPackageInfo == null)
            {
                AnsiConsole.MarkupLine($"[red]ERROR:[/] Failed to get package info for {v.VcpkgName}:{runtimeInfo.Triplet}");
                Environment.Exit(1);
                return;
            }

            var vcpkgInstalledPackageOutput = JsonSerializer.Deserialize<VcpkgInstalledPackageOutput>(vcpkgPackageInfo);

            context.Log.Information(vcpkgInstalledPackageOutput);
        }
    }
}

public record RuntimeInfo
{
    [JsonPropertyName("rid")]
    public required string Rid { get; init; }

    [JsonPropertyName("triplet")]
    public required string Triplet { get; init; }

    [JsonPropertyName("runner")]
    public required string Runner { get; init; }

    [JsonPropertyName("container_image")]
    public string? ContainerImage { get; init; }
}

public record RuntimeConfig
{
    [JsonPropertyName("runtimes")]
    public required IImmutableList<RuntimeInfo> Runtimes { get; init; }
}

public record VersionInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("vcpkg_name")]
    public required string VcpkgName { get; init; }

    [JsonPropertyName("vcpkg_version")]
    public required string VcpkgVersion { get; init; }

    [JsonPropertyName("vcpkg_port_version")]
    public required int VcpkgPortVersion { get; init; }

    [JsonPropertyName("native_lib_name")]
    public required string NativeLibName { get; init; }

    [JsonPropertyName("native_lib_version")]
    public required string NativeLibVersion { get; init; }

    [JsonPropertyName("core_lib")]
    public required bool CoreLib { get; init; }
}

public record VersionMappingConfig
{
    [JsonPropertyName("version_mapping")]
    public required IImmutableList<VersionInfo> VersionMapping { get; init; }
}

// This is the root object
public record VcpkgInstalledPackageOutput
{
    [JsonPropertyName("results")]
    public required ImmutableDictionary<string, VcpkgInstalledResult> Results { get; init; }
}

public record VcpkgInstalledResult
{
    [JsonPropertyName("version-string")]
    public required string VersionString { get; init; }

    [JsonPropertyName("port-version")]
    public required int PortVersion { get; init; }

    [JsonPropertyName("triplet")]
    public required string Triplet { get; init; }

    [JsonPropertyName("abi")]
    public string? Abi { get; init; }

    [JsonPropertyName("dependencies")]
    public required IImmutableList<string> Dependencies { get; init; }

    [JsonPropertyName("features")]
    public IImmutableList<string>? Features { get; init; }

    [JsonPropertyName("usage")]
    public string? Usage { get; init; }

    [JsonPropertyName("owns")]
    public required IImmutableList<string> Owns { get; init; }
}
