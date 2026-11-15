using Cake.Core.IO;

namespace Build.Host.Paths;

/// <summary>
/// Provides centralized, semantic path construction.
/// </summary>
public interface IPathService
{
    public DirectoryPath RepoRoot { get; }
    public DirectoryPath BuildDir { get; }
    public DirectoryPath BuildProjectDir { get; }
    public FilePath BuildProjectFile { get; }
    public DirectoryPath ArtifactsDir { get; }
    public DirectoryPath HarvestOutput { get; }
    public DirectoryPath PackagesOutput { get; }
    public DirectoryPath PackageConsumerSmokeOutput { get; }
    public DirectoryPath SmokeTestResultsOutput { get; }
    public FilePath GetPackageOutputFile(string packageId, string version);
    public DirectoryPath SrcDir { get; }
    public DirectoryPath VcpkgRoot { get; }
    public DirectoryPath VcpkgOverlayPortsDir { get; }
    public DirectoryPath VcpkgOverlayTripletsDir { get; }
    public FilePath VcpkgWindowsExecutableFile { get; }
    public FilePath VcpkgUnixExecutableFile { get; }
    public FilePath VcpkgBootstrapBatchScript { get; }
    public FilePath VcpkgBootstrapShellScript { get; }
    public DirectoryPath GetVcpkgInstalledDir { get; }
    public DirectoryPath GetVcpkgInstalledTripletDir(string triplet);
    public DirectoryPath GetVcpkgInstalledBinDir(string triplet);
    public DirectoryPath GetVcpkgInstalledLibDir(string triplet);
    public DirectoryPath GetVcpkgInstalledShareDir(string triplet);
    public DirectoryPath GetVcpkgPackageShareDir(string triplet, string packageName);
    public FilePath GetVcpkgPackageCopyrightFile(string triplet, string packageName);

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
    public string GetSdl2DynapiExportsGlob();

    /// <summary>
    /// Glob pattern that matches SDL2's dynapi manifest under vcpkg's installed buildtree.
    /// Expands to <c>{GetVcpkgInstalledDir}/vcpkg/blds/sdl2/src/*/src/dynapi/SDL2.exports</c>;
    /// the wildcard matches the version-tagged extraction directory vcpkg creates
    /// during a real (non-cache-hit) source build. Like <see cref="GetSdl2DynapiExportsGlob"/>,
    /// this is an SDL2-Core-only source oracle, not a satellite or SDL3 manifest.
    /// </summary>
    public string GetSdl2InstalledDynapiExportsGlob();

    public DirectoryPath GetHarvestStageDir(string libraryName, string rid);
    public DirectoryPath GetHarvestStageNativeDir(string libraryName, string rid);
    public DirectoryPath GetHarvestStageLicensesDir(string libraryName, string rid);
    public FilePath GetHarvestManifestFile(string libraryName, string rid);
    public FilePath GetManifestFile();
    public FilePath GetVcpkgManifestFile();
    public FilePath SolutionFile { get; }

    /// <summary>
    /// tests/smoke-tests/native-smoke
    /// </summary>
    public DirectoryPath NativeSmokeProjectDir { get; }

    /// <summary>
    /// tests/smoke-tests/native-smoke/build
    /// </summary>
    public DirectoryPath NativeSmokeBuildRoot { get; }

    /// <summary>
    /// tests/smoke-tests/native-smoke/build/{preset}
    /// </summary>
    public DirectoryPath GetNativeSmokeBuildPresetDir(string preset);

    /// <summary>
    /// tests/smoke-tests/native-smoke/build/{preset}/native-smoke(.exe)
    /// </summary>
    public FilePath GetNativeSmokeExecutableFile(string preset);

    /// <summary>
    /// artifacts/temp/inspect
    /// </summary>
    public DirectoryPath InspectOutputRoot { get; }

    /// <summary>
    /// artifacts/temp/inspect/{rid}
    /// </summary>
    public DirectoryPath GetInspectOutputRidDir(string rid);

    /// <summary>
    /// artifacts/temp/inspect/{rid}/{library}
    /// </summary>
    public DirectoryPath GetInspectOutputLibraryDir(string rid, string libraryName);

    /// <summary>
    /// artifacts/matrix/
    /// </summary>
    public DirectoryPath MatrixOutputRoot { get; }

    /// <summary>
    /// artifacts/matrix/runtimes.json
    /// </summary>
    public FilePath GetMatrixOutputFile();

    /// <summary>
    /// artifacts/harvest-staging/ — ephemeral per-RID pack staging root.
    /// </summary>
    public DirectoryPath HarvestStagingRoot { get; }

    // Per-library harvest output surface. Single source of truth for every per-library
    // path under artifacts/harvest_output/{library}/. Tasks that read or write these
    // paths should go through these accessors rather than composing `Combine("...")`
    // strings locally — keeps the on-disk layout governed by PathService and prevents
    // drift like the pre-H1 library-flat licenses layout that confused the pack gate.

    /// <summary>artifacts/harvest_output/{libraryName}/</summary>
    public DirectoryPath GetHarvestLibraryDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/runtimes/</summary>
    public DirectoryPath GetHarvestLibraryRuntimesDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/runtimes/{rid}/ — the per-RID directory Harvest cleans on re-run.</summary>
    public DirectoryPath GetHarvestLibraryRidRuntimesDir(string libraryName, string rid);

    /// <summary>artifacts/harvest_output/{libraryName}/licenses/ — parent of per-RID evidence AND the consolidated subtree.</summary>
    public DirectoryPath GetHarvestLibraryLicensesDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/licenses/{rid}/ — per-RID license evidence (post-H1 RID-scoped layout).</summary>
    public DirectoryPath GetHarvestLibraryRidLicensesDir(string libraryName, string rid);

    /// <summary>artifacts/harvest_output/{libraryName}/licenses/_consolidated/ — the pack-side input consumed by the native csproj.</summary>
    public DirectoryPath GetHarvestLibraryConsolidatedLicensesDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/licenses/_consolidated.tmp/ — Phase 1 staging target for the staged-replace swap.</summary>
    public DirectoryPath GetHarvestLibraryConsolidatedLicensesTempDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/rid-status/ — per-RID harvest status directory.</summary>
    public DirectoryPath GetHarvestLibraryRidStatusDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/rid-status/{rid}.json — per-RID harvest status file.</summary>
    public FilePath GetHarvestLibraryRidStatusFile(string libraryName, string rid);

    /// <summary>artifacts/harvest_output/{libraryName}/harvest-manifest.json — cross-RID consolidation receipt.</summary>
    public FilePath GetHarvestLibraryManifestFile(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/harvest-manifest.tmp.json — Phase 1 temp receipt for staged-replace.</summary>
    public FilePath GetHarvestLibraryManifestTempFile(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/harvest-summary.json — operator-facing consolidation summary.</summary>
    public FilePath GetHarvestLibrarySummaryFile(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/harvest-summary.tmp.json — Phase 1 temp summary for staged-replace.</summary>
    public FilePath GetHarvestLibrarySummaryTempFile(string libraryName);

    /// <summary>
    /// artifacts/harvest_output/{libraryName}/janset-native-metadata.json — per-family
    /// machine-readable upstream/version provenance generated before native pack.
    /// </summary>
    public FilePath GetHarvestLibraryNativeMetadataFile(string libraryName);

    /// <summary>
    /// Repository root README used by the generated upstream mapping table guardrail.
    /// </summary>
    public FilePath GetReadmeFile();

    /// <summary>
    /// Root for the smoke / example consumer surface. Individual smoke projects live under
    /// a family-scoped subdirectory (<c>package-smoke/</c> today, <c>examples/</c> future).
    /// </summary>
    public DirectoryPath SmokeTestsRoot { get; }

    /// <summary>
    /// Consumer-side TUnit smoke project exercised by <c>PackageConsumerSmoke</c>.
    /// </summary>
    public FilePath PackageConsumerSmokeProject { get; }

    /// <summary>
    /// Compile-only sanity project that validates the netstandard2.0 consumer surface.
    /// </summary>
    public FilePath CompileSanityProject { get; }
}
