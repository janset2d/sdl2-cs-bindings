using Build.Context.Configs;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Modules;

/// <summary>
/// Provides centralized, semantic path construction.
/// </summary>
public sealed class PathService
{
    private readonly DirectoryPath _repoRoot;
    private readonly DirectoryPath _vcpkgRoot;

    public PathService(RepositoryConfiguration repoConfiguration, ParsedArguments parsedArguments, ICakeLog log)
    {
        ArgumentNullException.ThrowIfNull(repoConfiguration);
        ArgumentNullException.ThrowIfNull(parsedArguments);
        ArgumentNullException.ThrowIfNull(log);

        _repoRoot = repoConfiguration.RepoRoot;

        // Determine Vcpkg Root Path
        if (parsedArguments.VcpkgDir?.Exists == true)
        {
            _vcpkgRoot = new DirectoryPath(parsedArguments.VcpkgDir.FullName);
            log.Information($"Using Vcpkg directory from settings/argument: {_vcpkgRoot.FullPath}");
        }
        else
        {
            // Default: Assume vcpkg is a submodule in the repo root
            _vcpkgRoot = _repoRoot.Combine("vcpkg");
            log.Warning($"Warning: Vcpkg directory not specified via --vcpkg-dir. Assuming relative path: {_vcpkgRoot.FullPath}");
        }
    }

    public DirectoryPath RepoRoot => _repoRoot;

    public DirectoryPath BuildProjectDir => RepoRoot.Combine("build").Combine("_build");

    public DirectoryPath ArtifactsDir => RepoRoot.Combine("artifacts");

    public DirectoryPath SrcDir => RepoRoot.Combine("src");

    public DirectoryPath VcpkgRoot => _vcpkgRoot;

    public DirectoryPath GetVcpkgInstalledDir(string triplet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);
        return VcpkgRoot.Combine("installed").Combine(triplet);
    }

    public DirectoryPath GetVcpkgInstalledBinDir(string triplet)
    {
        return GetVcpkgInstalledDir(triplet).Combine("bin");
    }

    public DirectoryPath GetVcpkgInstalledLibDir(string triplet)
    {
        return GetVcpkgInstalledDir(triplet).Combine("lib");
    }

    public DirectoryPath GetVcpkgInstalledShareDir(string triplet)
    {
        return GetVcpkgInstalledDir(triplet).Combine("share");
    }

    public DirectoryPath GetVcpkgPackageShareDir(string triplet, string packageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        return GetVcpkgInstalledShareDir(triplet).Combine(packageName);
    }

    public FilePath GetVcpkgPackageCopyrightFile(string triplet, string packageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);

        return GetVcpkgPackageShareDir(triplet, packageName).CombineWithFilePath("copyright");
    }

    public DirectoryPath GetHarvestStageDir(string libraryName, string rid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);

        return ArtifactsDir.Combine("harvest-staging").Combine($"{libraryName}-{rid}");
    }

    public DirectoryPath GetHarvestStageNativeDir(string libraryName, string rid)
    {
        return GetHarvestStageDir(libraryName, rid).Combine("runtimes").Combine(rid).Combine("native");
    }

    public DirectoryPath GetHarvestStageLicensesDir(string libraryName, string rid)
    {
        return GetHarvestStageDir(libraryName, rid).Combine("licenses");
    }

    public FilePath GetHarvestManifestFile(string libraryName, string rid)
    {
        return ArtifactsDir.CombineWithFilePath($"harvest-{libraryName}-{rid}.json");
    }
}
