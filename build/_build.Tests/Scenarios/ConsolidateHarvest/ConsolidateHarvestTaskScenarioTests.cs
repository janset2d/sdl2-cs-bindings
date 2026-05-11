using System.Text.Json;
using Build.Data;
using Build.Data.Harvest;
using Build.Host.Cake;
using Build.Targets.ConsolidateHarvest;
using Build.Tests.Fixtures;
using Cake.Core.Diagnostics;

namespace Build.Tests.Scenarios.ConsolidateHarvest;

/// <summary>
/// In-process behavior coverage for <c>ConsolidateHarvestTask</c>: exercises real task
/// orchestration against the fake Cake world. Covers precondition gates (harvest output
/// missing / empty), per-library handling (skip when no rid-status, fail-aggregate on
/// invalid JSON), the staged-replace manifest write, and the license union with divergence
/// detection. Behavior is asserted against the fake filesystem outputs (<c>harvest-manifest.json</c>,
/// <c>licenses/_consolidated/</c>) so the staged-replace contract is verified end-to-end.
/// </summary>
public sealed class ConsolidateHarvestTaskScenarioTests
{
    private const string LibraryName = "sdl2-core";
    private const string RidWindows = "win-x64";
    private const string RidLinux = "linux-x64";

    [Test]
    public async Task RunAsync_Should_Throw_When_Harvest_Output_Root_Is_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("harvest output root", StringComparison.Ordinal);
        await Assert.That(result.Exception!.Message).Contains("is missing", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Harvest_Output_Has_No_Library_Directories()
    {
        // Harvest root directory exists but contains nothing.
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("artifacts/harvest_output/.placeholder", string.Empty);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("contains no library directories", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Skip_Library_With_No_Rid_Status_Files()
    {
        // Library dir exists but rid-status/ is empty -> repository returns null -> reporter logs
        // skip + info, no failure aggregation.
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/.placeholder", string.Empty);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Log.HasMessage(LogLevel.Information, "No RID status files found")).IsTrue();
        // No manifest written because we skipped.
        var manifestRelative = $"artifacts/harvest_output/{LibraryName}/harvest-manifest.json";
        await Assert.That(world.FileExists(manifestRelative)).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Aggregate_Failure_When_Rid_Status_File_Is_Invalid_Json()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/rid-status/{RidWindows}.json", "{ not valid json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("ConsolidateHarvest failed for 1", StringComparison.Ordinal);
        await Assert.That(result.Exception!.Message).Contains("unreadable or invalid", StringComparison.Ordinal);
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "Failed to consolidate harvest for library")).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Write_Manifest_With_Success_Stats_When_All_Rids_Succeed()
    {
        // Two RIDs, both successful, no divergent licenses.
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/rid-status/{RidWindows}.json", SuccessRidStatus(RidWindows, "x64-windows-hybrid"))
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/rid-status/{RidLinux}.json", SuccessRidStatus(RidLinux, "x64-linux-hybrid"));

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Log.HasMessage(LogLevel.Information, "Harvest consolidation completed successfully")).IsTrue();

        var manifestRelative = $"artifacts/harvest_output/{LibraryName}/harvest-manifest.json";
        await Assert.That(world.FileExists(manifestRelative)).IsTrue();
        var manifest = await world.CakeContext.ToJsonAsync<HarvestManifest>(world.RepoRoot.CombineWithFilePath(manifestRelative));
        await Assert.That(manifest.LibraryName).IsEqualTo(LibraryName);
        await Assert.That(manifest.Summary.TotalRids).IsEqualTo(2);
        await Assert.That(manifest.Summary.SuccessfulRids).IsEqualTo(2);
        await Assert.That(manifest.Summary.FailedRids).IsEqualTo(0);

        // Tmp staging files should have been swapped away — final exists, tmp does not.
        var manifestTempRelative = $"artifacts/harvest_output/{LibraryName}/harvest-manifest.tmp.json";
        await Assert.That(world.FileExists(manifestTempRelative)).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Detect_License_Divergence_Across_Rids()
    {
        // Same package "zlib", same file "copyright", different content per RID → divergence.
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/rid-status/{RidWindows}.json", SuccessRidStatus(RidWindows, "x64-windows-hybrid"))
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/rid-status/{RidLinux}.json", SuccessRidStatus(RidLinux, "x64-linux-hybrid"))
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/licenses/{RidWindows}/zlib/copyright", "windows-flavored copyright text")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/licenses/{RidLinux}/zlib/copyright", "linux-flavored copyright text");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Log.HasMessage(LogLevel.Warning, "License divergence detected")).IsTrue();

        var manifestRelative = $"artifacts/harvest_output/{LibraryName}/harvest-manifest.json";
        var manifest = await world.CakeContext.ToJsonAsync<HarvestManifest>(world.RepoRoot.CombineWithFilePath(manifestRelative));
        await Assert.That(manifest.Consolidation).IsNotNull();
        await Assert.That(manifest.Consolidation!.LicensesConsolidated).IsTrue();
        await Assert.That(manifest.Consolidation!.DivergentLicenses.Count).IsEqualTo(1);
        await Assert.That(manifest.Consolidation!.DivergentLicenses[0].Package).IsEqualTo("zlib");
        await Assert.That(manifest.Consolidation!.DivergentLicenses[0].FileName).IsEqualTo("copyright");

        // Per-RID variants written under the package dir.
        await Assert.That(world.FileExists($"artifacts/harvest_output/{LibraryName}/licenses/_consolidated/zlib/copyright.{RidWindows}")).IsTrue();
        await Assert.That(world.FileExists($"artifacts/harvest_output/{LibraryName}/licenses/_consolidated/zlib/copyright.{RidLinux}")).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Collapse_Identical_Licenses_To_Canonical_Path()
    {
        // Same content across RIDs → single canonical entry, no divergence record.
        const string sharedContent = "shared zlib copyright text";
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/rid-status/{RidWindows}.json", SuccessRidStatus(RidWindows, "x64-windows-hybrid"))
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/rid-status/{RidLinux}.json", SuccessRidStatus(RidLinux, "x64-linux-hybrid"))
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/licenses/{RidWindows}/zlib/copyright", sharedContent)
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/licenses/{RidLinux}/zlib/copyright", sharedContent);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsTrue();

        var manifestRelative = $"artifacts/harvest_output/{LibraryName}/harvest-manifest.json";
        var manifest = await world.CakeContext.ToJsonAsync<HarvestManifest>(world.RepoRoot.CombineWithFilePath(manifestRelative));
        await Assert.That(manifest.Consolidation!.DivergentLicenses.Count).IsEqualTo(0);

        // Canonical path written; per-RID variants NOT written.
        await Assert.That(world.FileExists($"artifacts/harvest_output/{LibraryName}/licenses/_consolidated/zlib/copyright")).IsTrue();
        await Assert.That(world.FileExists($"artifacts/harvest_output/{LibraryName}/licenses/_consolidated/zlib/copyright.{RidWindows}")).IsFalse();
    }

    private static string SuccessRidStatus(string rid, string triplet)
    {
        var status = new RidHarvestStatus
        {
            LibraryName = LibraryName,
            Rid = rid,
            Triplet = triplet,
            Success = true,
            ErrorMessage = null,
            Timestamp = DateTimeOffset.UtcNow,
            Statistics = new HarvestStatistics
            {
                PrimaryFilesCount = 1,
                RuntimeFilesCount = 0,
                LicenseFilesCount = 1,
                DeployedPackagesCount = 1,
                FilteredPackagesCount = 0,
                DeploymentStrategy = "DirectCopy",
            },
        };
        return JsonSerializer.Serialize(status, CakeJsonExtensions.DefaultJsonOptions);
    }

    private static TargetTestHostV2<ConsolidateHarvestTask> CreateHost(FakeCakeWorldV2 world)
    {
        var host = new TargetTestHostV2<ConsolidateHarvestTask>(world);
        return host.WithServices(services => services
            .AddData()
            .AddConsolidateHarvest());
    }
}
