using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Build.Host.Cake;
using Build.Results;
using Cake.Core.IO;

namespace Build.Tools.Vcpkg;

public static class VcpkgPackageInfoParser
{
    public static Result<VcpkgPackageInfo, VcpkgPackageInfoError> ParseInstalledPackageInfo(
        string? jsonOutput,
        string packageName,
        string triplet,
        DirectoryPath installedRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);
        ArgumentNullException.ThrowIfNull(installedRoot);

        var packageKey = VcpkgPackageKey.Create(packageName, triplet);

        if (string.IsNullOrWhiteSpace(jsonOutput))
        {
            return Result<VcpkgPackageInfo, VcpkgPackageInfoError>.Failure(
                new VcpkgPackageInfoError($"Vcpkg x-package-info returned no output for {packageKey}."));
        }

        try
        {
            var vcpkgInstalledOutput = CakeJsonExtensions.DeserializeJson<VcpkgPackageInfoOutput>(jsonOutput);
            if (vcpkgInstalledOutput?.Results is null || !vcpkgInstalledOutput.Results.TryGetValue(packageKey, out var packageResult))
            {
                return Result<VcpkgPackageInfo, VcpkgPackageInfoError>.Failure(
                    new VcpkgPackageInfoError($"Failed to deserialize or find package info for {packageKey} in vcpkg output."));
            }

            var ownedFiles = packageResult.Owns
                .Select(relativeChildPath => installedRoot.CombineWithFilePath(relativeChildPath).FullPath)
                .ToImmutableList();

            return Result<VcpkgPackageInfo, VcpkgPackageInfoError>.Success(
                new VcpkgPackageInfo(
                    PackageName: packageName,
                    Triplet: triplet,
                    OwnedFiles: ownedFiles,
                    DeclaredDependencies: packageResult.Dependencies ?? ImmutableList<string>.Empty));
        }
        catch (JsonException ex)
        {
            return BuildPackageInfoError(ex);
        }
        catch (NotSupportedException ex)
        {
            return BuildPackageInfoError(ex);
        }
        catch (ArgumentException ex)
        {
            return BuildPackageInfoError(ex);
        }
        catch (FormatException ex)
        {
            return BuildPackageInfoError(ex);
        }
    }

    private static Result<VcpkgPackageInfo, VcpkgPackageInfoError> BuildPackageInfoError(Exception exception)
    {
        return Result<VcpkgPackageInfo, VcpkgPackageInfoError>.Failure(
            new VcpkgPackageInfoError($"Error building dependency closure: {exception.Message}", exception));
    }

    internal sealed record VcpkgPackageInfoOutput
    {
        [JsonPropertyName("results")]
        public required ImmutableDictionary<string, VcpkgInstalledResult> Results { get; init; }
    }

    internal sealed record VcpkgInstalledResult
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
        public IImmutableList<string>? Dependencies { get; init; } = ImmutableList<string>.Empty;

        [JsonPropertyName("features")]
        public IImmutableList<string>? Features { get; init; }

        [JsonPropertyName("usage")]
        public string? Usage { get; init; }

        [JsonPropertyName("owns")]
        public required IImmutableList<string> Owns { get; init; }
    }
}

internal static class VcpkgPackageKey
{
    public static string Create(string packageName, string triplet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);

        return $"{packageName}:{triplet}";
    }
}
