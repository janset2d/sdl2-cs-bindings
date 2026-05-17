using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Host.Paths;

/// <summary>
/// Provides centralized, semantic path construction.
/// </summary>
public interface IPathService
{
    DirectoryPath RepoRoot { get; }
    DirectoryPath BuildDir { get; }
    DirectoryPath BuildProjectDir { get; }
    FilePath BuildProjectFile { get; }
    DirectoryPath ArtifactsDir { get; }
    DirectoryPath HarvestOutput { get; }
    DirectoryPath PackagesOutput { get; }
    DirectoryPath PackageConsumerSmokeOutput { get; }
    DirectoryPath SmokeTestResultsOutput { get; }
    FilePath GetPackageOutputFile(string packageId, string version);
    DirectoryPath SrcDir { get; }
    DirectoryPath VcpkgRoot { get; }
    DirectoryPath VcpkgOverlayPortsDir { get; }
    DirectoryPath VcpkgOverlayTripletsDir { get; }
    FilePath VcpkgWindowsExecutableFile { get; }
    FilePath VcpkgUnixExecutableFile { get; }
    FilePath VcpkgBootstrapBatchScript { get; }
    FilePath VcpkgBootstrapShellScript { get; }
    DirectoryPath GetVcpkgInstalledDir { get; }
    DirectoryPath GetVcpkgInstalledTripletDir(string triplet);
    DirectoryPath GetVcpkgInstalledBinDir(string triplet);
    DirectoryPath GetVcpkgInstalledLibDir(string triplet);
    DirectoryPath GetVcpkgInstalledShareDir(string triplet);
    DirectoryPath GetVcpkgPackageShareDir(string triplet, string packageName);
    FilePath GetVcpkgPackageCopyrightFile(string triplet, string packageName);

    /// <summary>
    /// Glob pattern that matches SDL2's dynapi manifest under vcpkg's buildtree.
    /// Expands to <c>{VcpkgRoot}/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports</c>;
    /// the wildcard matches the version-tagged extraction directory vcpkg creates
    /// during a real (non-cache-hit) source build.
    /// <para>
    /// The <c>sdl2</c> port-name segment is intentional — the dynapi (dynamic-API
    /// dispatch table) is an SDL2-Core-only feature. SDL2 satellites (sdl2-image /
    /// sdl2-mixer / sdl2-ttf / sdl2-net / sdl2-gfx) do not have dynapi manifests,
    /// and SDL3 removed the dynapi system entirely. When SDL3 support lands, a
    /// distinct accessor (with its own SDL3-appropriate symbol oracle) is the
    /// right shape, not a parameterized version of this one.
    /// </para>
    /// </summary>
    string GetSdl2DynapiExportsGlob();

    DirectoryPath GetHarvestStageDir(string libraryName, string rid);
    DirectoryPath GetHarvestStageNativeDir(string libraryName, string rid);
    DirectoryPath GetHarvestStageLicensesDir(string libraryName, string rid);
    FilePath GetHarvestManifestFile(string libraryName, string rid);
    FilePath GetManifestFile();
    FilePath GetVcpkgManifestFile();
    FilePath SolutionFile { get; }

    /// <summary>
    /// tests/smoke-tests/native-smoke
    /// </summary>
    DirectoryPath NativeSmokeProjectDir { get; }

    /// <summary>
    /// tests/smoke-tests/native-smoke/build
    /// </summary>
    DirectoryPath NativeSmokeBuildRoot { get; }

    /// <summary>
    /// tests/smoke-tests/native-smoke/build/{preset}
    /// </summary>
    DirectoryPath GetNativeSmokeBuildPresetDir(string preset);

    /// <summary>
    /// tests/smoke-tests/native-smoke/build/{preset}/native-smoke(.exe)
    /// </summary>
    FilePath GetNativeSmokeExecutableFile(string preset);

    /// <summary>
    /// artifacts/temp/inspect
    /// </summary>
    DirectoryPath InspectOutputRoot { get; }

    /// <summary>
    /// artifacts/temp/inspect/{rid}
    /// </summary>
    DirectoryPath GetInspectOutputRidDir(string rid);

    /// <summary>
    /// artifacts/temp/inspect/{rid}/{library}
    /// </summary>
    DirectoryPath GetInspectOutputLibraryDir(string rid, string libraryName);

    /// <summary>
    /// artifacts/generated-bindings-preview/ — Stage 1 scratch loop output root.
    /// Temporary scaffolding; retires when Task 5/7 of the Stage 1 plan lands real
    /// emitters and the production-location flag-flip to src/SDL2.{Family}/Generated/.
    /// Do not consume outside build/_build/Targets/GenerateBindings/ or
    /// tools.cs generate-bindings.
    /// </summary>
    DirectoryPath GenerateBindingsPreviewRoot { get; }

    /// <summary>
    /// artifacts/generated-bindings-preview/{family}/ — per-family scratch loop output.
    /// See <see cref="GenerateBindingsPreviewRoot"/> for retirement criteria.
    /// </summary>
    DirectoryPath GetGenerateBindingsPreviewFamilyRoot(string family);

    /// <summary>
    /// build/_build/Targets/GenerateBindings/SyntheticHeaders/ — empty stub system
    /// headers added to the parser's SystemIncludeFolders so SDL2 headers that
    /// reference Windows / Apple / WinRT system headers parse cleanly on Linux.
    /// See the README in that directory for retirement criteria.
    /// </summary>
    DirectoryPath BindingGeneratorSyntheticHeadersRoot { get; }

    /// <summary>
    /// artifacts/matrix/
    /// </summary>
    DirectoryPath MatrixOutputRoot { get; }

    /// <summary>
    /// artifacts/matrix/runtimes.json
    /// </summary>
    FilePath GetMatrixOutputFile();

    /// <summary>
    /// artifacts/harvest-staging/ — ephemeral per-RID pack staging root.
    /// </summary>
    DirectoryPath HarvestStagingRoot { get; }

    // Per-library harvest output surface. Single source of truth for every per-library
    // path under artifacts/harvest_output/{library}/. Tasks that read or write these
    // paths should go through these accessors rather than composing `Combine("...")`
    // strings locally — keeps the on-disk layout governed by PathService and prevents
    // drift like the pre-H1 library-flat licenses layout that confused the pack gate.

    /// <summary>artifacts/harvest_output/{libraryName}/</summary>
    DirectoryPath GetHarvestLibraryDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/runtimes/</summary>
    DirectoryPath GetHarvestLibraryRuntimesDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/runtimes/{rid}/ — the per-RID directory Harvest cleans on re-run.</summary>
    DirectoryPath GetHarvestLibraryRidRuntimesDir(string libraryName, string rid);

    /// <summary>artifacts/harvest_output/{libraryName}/licenses/ — parent of per-RID evidence AND the consolidated subtree.</summary>
    DirectoryPath GetHarvestLibraryLicensesDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/licenses/{rid}/ — per-RID license evidence (post-H1 RID-scoped layout).</summary>
    DirectoryPath GetHarvestLibraryRidLicensesDir(string libraryName, string rid);

    /// <summary>artifacts/harvest_output/{libraryName}/licenses/_consolidated/ — the pack-side input consumed by the native csproj.</summary>
    DirectoryPath GetHarvestLibraryConsolidatedLicensesDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/licenses/_consolidated.tmp/ — Phase 1 staging target for the staged-replace swap.</summary>
    DirectoryPath GetHarvestLibraryConsolidatedLicensesTempDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/rid-status/ — per-RID harvest status directory.</summary>
    DirectoryPath GetHarvestLibraryRidStatusDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/rid-status/{rid}.json — per-RID harvest status file.</summary>
    FilePath GetHarvestLibraryRidStatusFile(string libraryName, string rid);

    /// <summary>artifacts/harvest_output/{libraryName}/harvest-manifest.json — cross-RID consolidation receipt.</summary>
    FilePath GetHarvestLibraryManifestFile(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/harvest-manifest.tmp.json — Phase 1 temp receipt for staged-replace.</summary>
    FilePath GetHarvestLibraryManifestTempFile(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/harvest-summary.json — operator-facing consolidation summary.</summary>
    FilePath GetHarvestLibrarySummaryFile(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/harvest-summary.tmp.json — Phase 1 temp summary for staged-replace.</summary>
    FilePath GetHarvestLibrarySummaryTempFile(string libraryName);

    /// <summary>
    /// artifacts/harvest_output/{libraryName}/janset-native-metadata.json — per-family
    /// machine-readable upstream/version provenance generated before native pack.
    /// </summary>
    FilePath GetHarvestLibraryNativeMetadataFile(string libraryName);

    /// <summary>
    /// Repository root README used by the generated upstream mapping table guardrail.
    /// </summary>
    FilePath GetReadmeFile();

    /// <summary>
    /// Root for the smoke / example consumer surface. Individual smoke projects live under
    /// a family-scoped subdirectory (<c>package-smoke/</c> today, <c>examples/</c> future).
    /// </summary>
    DirectoryPath SmokeTestsRoot { get; }

    /// <summary>
    /// Consumer-side TUnit smoke project exercised by <c>PackageConsumerSmoke</c>.
    /// </summary>
    FilePath PackageConsumerSmokeProject { get; }

    /// <summary>
    /// Compile-only sanity project that validates the netstandard2.0 consumer surface.
    /// </summary>
    FilePath CompileSanityProject { get; }
}

/// <inheritdoc />
public sealed class PathService : IPathService
{
    private readonly DirectoryPath _repoRoot;
    private readonly DirectoryPath _vcpkgRoot;
    private readonly DirectoryPath _vcpkgInstalledDir;

    public PathService(DirectoryPath repoRoot, ParsedArguments parsedArguments, ICakeLog log)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);
        ArgumentNullException.ThrowIfNull(parsedArguments);
        ArgumentNullException.ThrowIfNull(log);

        _repoRoot = repoRoot;

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

    public DirectoryPath PackageConsumerSmokeOutput => ArtifactsDir.Combine("package-consumer-smoke");

    public DirectoryPath SmokeTestResultsOutput => ArtifactsDir.Combine("test-results").Combine("smoke");

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

    public string GetSdl2DynapiExportsGlob()
    {
        return VcpkgRoot.Combine("buildtrees/sdl2/src/*/src/dynapi/SDL2.exports").FullPath;
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

    public FilePath SolutionFile => RepoRoot.CombineWithFilePath("Janset.SDL2.sln");

    public DirectoryPath NativeSmokeProjectDir =>
        RepoRoot.Combine("tests").Combine("smoke-tests").Combine("native-smoke");

    public DirectoryPath NativeSmokeBuildRoot => NativeSmokeProjectDir.Combine("build");

    public DirectoryPath GetNativeSmokeBuildPresetDir(string preset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preset);
        return NativeSmokeBuildRoot.Combine(preset);
    }

    public FilePath GetNativeSmokeExecutableFile(string preset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preset);
        var executableName = preset.StartsWith("win-", StringComparison.OrdinalIgnoreCase)
            ? "native-smoke.exe"
            : "native-smoke";
        return GetNativeSmokeBuildPresetDir(preset).CombineWithFilePath(executableName);
    }

    public DirectoryPath InspectOutputRoot => ArtifactsDir.Combine("temp").Combine("inspect");

    public DirectoryPath GetInspectOutputRidDir(string rid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);
        return InspectOutputRoot.Combine(rid);
    }

    public DirectoryPath GetInspectOutputLibraryDir(string rid, string libraryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        return GetInspectOutputRidDir(rid).Combine(libraryName);
    }

    public DirectoryPath GenerateBindingsPreviewRoot
        => ArtifactsDir.Combine("generated-bindings-preview");

    public DirectoryPath GetGenerateBindingsPreviewFamilyRoot(string family)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        return GenerateBindingsPreviewRoot.Combine(family);
    }

    public DirectoryPath BindingGeneratorSyntheticHeadersRoot
        => RepoRoot.Combine("build/_build/Targets/GenerateBindings/SyntheticHeaders");

    public DirectoryPath MatrixOutputRoot => ArtifactsDir.Combine("matrix");

    public FilePath GetMatrixOutputFile()
    {
        return MatrixOutputRoot.CombineWithFilePath("runtimes.json");
    }

    public DirectoryPath HarvestStagingRoot => ArtifactsDir.Combine("harvest-staging");

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

    public DirectoryPath SmokeTestsRoot => RepoRoot.Combine("tests").Combine("smoke-tests");

    public FilePath PackageConsumerSmokeProject =>
        SmokeTestsRoot.Combine("package-smoke").Combine("PackageConsumer.Smoke").CombineWithFilePath("PackageConsumer.Smoke.csproj");

    public FilePath CompileSanityProject =>
        SmokeTestsRoot.Combine("package-smoke").Combine("Compile.NetStandard").CombineWithFilePath("Compile.NetStandard.csproj");
}
