using Cake.Core.IO;

namespace Build.Domain.Paths;

public interface IPathService
{
    DirectoryPath RepoRoot { get; }
    DirectoryPath BuildDir { get; }
    DirectoryPath BuildProjectDir { get; }
    FilePath BuildProjectFile { get; }
    DirectoryPath ArtifactsDir { get; }
    DirectoryPath HarvestOutput { get; }
    DirectoryPath PackagesOutput { get; }
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
    DirectoryPath GetHarvestStageDir(string libraryName, string rid);
    DirectoryPath GetHarvestStageNativeDir(string libraryName, string rid);
    DirectoryPath GetHarvestStageLicensesDir(string libraryName, string rid);
    FilePath GetHarvestManifestFile(string libraryName, string rid);
    FilePath GetManifestFile();
    FilePath GetVcpkgManifestFile();
    FilePath GetCoverageBaselineFile();

    // Per-library harvest output surface. Single source of truth for every per-library
    // path under artifacts/harvest_output/{library}/. Tasks that read or write these
    // paths should go through these accessors rather than composing `Combine("...")`
    // strings locally — keeps the on-disk layout governed by PathService and prevents
    // drift like the pre-H1 library-flat licenses layout that confused the pack gate.

    /// <summary>artifacts/harvest_output/{libraryName}/</summary>
    DirectoryPath GetHarvestLibraryDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/runtimes/</summary>
    DirectoryPath GetHarvestLibraryRuntimesDir(string libraryName);

    /// <summary>artifacts/harvest_output/{libraryName}/runtimes/{rid}/ — the per-RID slice Harvest cleans on re-run.</summary>
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
    /// build/msbuild/Janset.Smoke.local.props — developer-local smoke override generated by SetupLocalDev.
    /// </summary>
    FilePath GetSmokeLocalPropsFile();

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
