using Build.Context.Configs;
using Build.Domain.Paths;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Infrastructure.Paths;

/// <summary>
/// Provides centralized, semantic path construction.
/// </summary>
public sealed class PathService : IPathService
{
    private readonly DirectoryPath _repoRoot;
    private readonly DirectoryPath _vcpkgRoot;
    private readonly DirectoryPath _vcpkgInstalledDir;

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
            _vcpkgRoot = _repoRoot.Combine("external").Combine("vcpkg");
            log.Warning($"Warning: Vcpkg directory not specified via --vcpkg-dir. Assuming relative path: {_vcpkgRoot.FullPath}");
        }

        var vcpkgInstalledDirInfo = parsedArguments.VcpkgInstalledDir != null ? new DirectoryInfo(parsedArguments.VcpkgInstalledDir.FullName) : null;

        if (vcpkgInstalledDirInfo?.Exists == true)
        {
            _vcpkgInstalledDir = new DirectoryPath(vcpkgInstalledDirInfo.FullName);
            log.Information($"Using Vcpkg installed directory from settings/argument: {_vcpkgInstalledDir.FullPath}");
        }
        else
        {
            _vcpkgInstalledDir = _repoRoot.Combine("vcpkg_installed");
            log.Warning($"Warning: Vcpkg installed directory not specified via --vcpkg-installed-dir. Assuming relative path: {_vcpkgInstalledDir.FullPath}");
        }
    }

    public DirectoryPath RepoRoot => _repoRoot;

    public DirectoryPath BuildDir => RepoRoot.Combine("build");

    public DirectoryPath BuildProjectDir => BuildDir.Combine("_build");

    public FilePath BuildProjectFile => BuildProjectDir.CombineWithFilePath("Build.csproj");

    public DirectoryPath ArtifactsDir => RepoRoot.Combine("artifacts");

    public DirectoryPath HarvestOutput => ArtifactsDir.Combine("harvest_output");

    public DirectoryPath PackagesOutput => ArtifactsDir.Combine("packages");

    public FilePath GetPackageOutputFile(string packageId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return PackagesOutput.CombineWithFilePath($"{packageId}.{version}.nupkg");
    }

    public DirectoryPath SrcDir => RepoRoot.Combine("src");

    public DirectoryPath VcpkgRoot => _vcpkgRoot;

    public DirectoryPath VcpkgOverlayPortsDir => RepoRoot.Combine("vcpkg-overlay-ports");

    public DirectoryPath VcpkgOverlayTripletsDir => RepoRoot.Combine("vcpkg-overlay-triplets");

    public FilePath VcpkgWindowsExecutableFile => VcpkgRoot.CombineWithFilePath("vcpkg.exe");

    public FilePath VcpkgUnixExecutableFile => VcpkgRoot.CombineWithFilePath("vcpkg");

    public FilePath VcpkgBootstrapBatchScript => VcpkgRoot.CombineWithFilePath("bootstrap-vcpkg.bat");

    public FilePath VcpkgBootstrapShellScript => VcpkgRoot.CombineWithFilePath("bootstrap-vcpkg.sh");

    public DirectoryPath GetVcpkgInstalledDir => _vcpkgInstalledDir;

    public DirectoryPath GetVcpkgInstalledTripletDir(string triplet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);
        return GetVcpkgInstalledDir.Combine(triplet);
    }

    public DirectoryPath GetVcpkgInstalledBinDir(string triplet)
    {
        return GetVcpkgInstalledTripletDir(triplet).Combine("bin");
    }

    public DirectoryPath GetVcpkgInstalledLibDir(string triplet)
    {
        return GetVcpkgInstalledTripletDir(triplet).Combine("lib");
    }

    public DirectoryPath GetVcpkgInstalledShareDir(string triplet)
    {
        return GetVcpkgInstalledTripletDir(triplet).Combine("share");
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

    public FilePath GetManifestFile()
    {
        return BuildDir.CombineWithFilePath("manifest.json");
    }

    public FilePath GetVcpkgManifestFile()
    {
        return RepoRoot.CombineWithFilePath("vcpkg.json");
    }

    public FilePath GetCoverageBaselineFile()
    {
        return BuildDir.CombineWithFilePath("coverage-baseline.json");
    }

    public DirectoryPath GetHarvestLibraryDir(string libraryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        return HarvestOutput.Combine(libraryName);
    }

    public DirectoryPath GetHarvestLibraryRuntimesDir(string libraryName)
    {
        return GetHarvestLibraryDir(libraryName).Combine("runtimes");
    }

    public DirectoryPath GetHarvestLibraryRidRuntimesDir(string libraryName, string rid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);
        return GetHarvestLibraryRuntimesDir(libraryName).Combine(rid);
    }

    public DirectoryPath GetHarvestLibraryLicensesDir(string libraryName)
    {
        return GetHarvestLibraryDir(libraryName).Combine("licenses");
    }

    public DirectoryPath GetHarvestLibraryRidLicensesDir(string libraryName, string rid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);
        return GetHarvestLibraryLicensesDir(libraryName).Combine(rid);
    }

    public DirectoryPath GetHarvestLibraryConsolidatedLicensesDir(string libraryName)
    {
        return GetHarvestLibraryLicensesDir(libraryName).Combine("_consolidated");
    }

    public DirectoryPath GetHarvestLibraryConsolidatedLicensesTempDir(string libraryName)
    {
        return GetHarvestLibraryLicensesDir(libraryName).Combine("_consolidated.tmp");
    }

    public DirectoryPath GetHarvestLibraryRidStatusDir(string libraryName)
    {
        return GetHarvestLibraryDir(libraryName).Combine("rid-status");
    }

    public FilePath GetHarvestLibraryRidStatusFile(string libraryName, string rid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);
        return GetHarvestLibraryRidStatusDir(libraryName).CombineWithFilePath($"{rid}.json");
    }

    public FilePath GetHarvestLibraryManifestFile(string libraryName)
    {
        return GetHarvestLibraryDir(libraryName).CombineWithFilePath("harvest-manifest.json");
    }

    public FilePath GetHarvestLibraryManifestTempFile(string libraryName)
    {
        return GetHarvestLibraryDir(libraryName).CombineWithFilePath("harvest-manifest.tmp.json");
    }

    public FilePath GetHarvestLibrarySummaryFile(string libraryName)
    {
        return GetHarvestLibraryDir(libraryName).CombineWithFilePath("harvest-summary.json");
    }

    public FilePath GetHarvestLibrarySummaryTempFile(string libraryName)
    {
        return GetHarvestLibraryDir(libraryName).CombineWithFilePath("harvest-summary.tmp.json");
    }

    public FilePath GetHarvestLibraryNativeMetadataFile(string libraryName)
    {
        return GetHarvestLibraryDir(libraryName).CombineWithFilePath("janset-native-metadata.json");
    }

    public FilePath GetReadmeFile()
    {
        return RepoRoot.CombineWithFilePath("README.md");
    }

    public FilePath GetSmokeLocalPropsFile()
    {
        return BuildDir.Combine("msbuild").CombineWithFilePath("Janset.Smoke.local.props");
    }

    public FilePath GetResolveVersionsOutputFile()
    {
        return ArtifactsDir.Combine("resolve-versions").CombineWithFilePath("versions.json");
    }

    public DirectoryPath SmokeTestsRoot => RepoRoot.Combine("tests").Combine("smoke-tests");

    public FilePath PackageConsumerSmokeProject =>
        SmokeTestsRoot.Combine("package-smoke").Combine("PackageConsumer.Smoke").CombineWithFilePath("PackageConsumer.Smoke.csproj");

    public FilePath CompileSanityProject =>
        SmokeTestsRoot.Combine("package-smoke").Combine("Compile.NetStandard").CombineWithFilePath("Compile.NetStandard.csproj");
}
