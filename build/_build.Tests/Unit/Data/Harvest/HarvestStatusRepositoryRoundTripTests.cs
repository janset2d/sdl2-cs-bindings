using System.Globalization;
using Build.Data.Harvest;
using Build.Host.Cake;
using Build.Targets.Harvest.Models;
using Build.Tests.Fixtures;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Tests.Unit.Data.Harvest;

/// <summary>
/// Sociable round-trip coverage for <see cref="HarvestStatusRepository"/> — exercises real
/// <see cref="ICakeContext"/> + <see cref="Cake.Testing.FakeFileSystem"/> + real JSON
/// serialization end-to-end, asserting on byte-on-disk parity for the rid-status JSON contract
/// that downstream consolidation + Pack consume. Pairs with
/// <see cref="HarvestStatusRepositoryUnitTests"/> per the repository-cohort rule.
/// </summary>
public sealed class HarvestStatusRepositoryRoundTripTests
{
    private const string LibraryName = "sdl2-core";
    private const string Rid = "win-x64";

    [Test]
    public async Task Invalidate_Should_Delete_Existing_Per_Rid_Runtime_Tree()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithRid(Rid)
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/runtimes/{Rid}/native/SDL2.dll", "fake");
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime, world.Clock);

        repo.Invalidate(LibraryName);

        var ridDir = ctx.Paths.GetHarvestLibraryRidRuntimesDir(LibraryName, Rid);
        await Assert.That(world.CakeContext.DirectoryExists(ridDir)).IsFalse();
    }

    [Test]
    public async Task Invalidate_Should_Delete_Existing_Per_Rid_Status_File()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithRid(Rid)
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/rid-status/{Rid}.json", "{}");
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime, world.Clock);

        repo.Invalidate(LibraryName);

        var statusFile = ctx.Paths.GetHarvestLibraryRidStatusFile(LibraryName, Rid);
        await Assert.That(world.CakeContext.FileExists(statusFile)).IsFalse();
    }

    [Test]
    public async Task Invalidate_Should_Delete_Existing_Per_Rid_Licenses_Tree()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithRid(Rid)
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/licenses/{Rid}/zlib/copyright", "MIT");
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime, world.Clock);

        repo.Invalidate(LibraryName);

        var ridLicensesDir = ctx.Paths.GetHarvestLibraryRidLicensesDir(LibraryName, Rid);
        await Assert.That(world.CakeContext.DirectoryExists(ridLicensesDir)).IsFalse();
    }

    [Test]
    public async Task Invalidate_Should_Delete_Cross_Rid_Consolidated_Artifacts()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithRid(Rid)
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/licenses/_consolidated/zlib/copyright", "MIT")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/licenses/_consolidated.tmp/zlib/copyright", "MIT")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/harvest-manifest.json", "{}")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/harvest-manifest.tmp.json", "{}")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/harvest-summary.json", "{}")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/harvest-summary.tmp.json", "{}");
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime, world.Clock);

        repo.Invalidate(LibraryName);

        await Assert.That(world.CakeContext.DirectoryExists(ctx.Paths.GetHarvestLibraryConsolidatedLicensesDir(LibraryName))).IsFalse();
        await Assert.That(world.CakeContext.DirectoryExists(ctx.Paths.GetHarvestLibraryConsolidatedLicensesTempDir(LibraryName))).IsFalse();
        await Assert.That(world.CakeContext.FileExists(ctx.Paths.GetHarvestLibraryManifestFile(LibraryName))).IsFalse();
        await Assert.That(world.CakeContext.FileExists(ctx.Paths.GetHarvestLibraryManifestTempFile(LibraryName))).IsFalse();
        await Assert.That(world.CakeContext.FileExists(ctx.Paths.GetHarvestLibrarySummaryFile(LibraryName))).IsFalse();
        await Assert.That(world.CakeContext.FileExists(ctx.Paths.GetHarvestLibrarySummaryTempFile(LibraryName))).IsFalse();
    }

    [Test]
    public async Task Invalidate_Should_Be_Noop_When_Nothing_To_Invalidate()
    {
        var world = FakeCakeWorld.CreateWindows().WithRid(Rid);
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime, world.Clock);

        repo.Invalidate(LibraryName);

        await Assert.That(world.CakeContext.DirectoryExists(ctx.Paths.GetHarvestLibraryRidRuntimesDir(LibraryName, Rid))).IsFalse();
    }

    [Test]
    public async Task WriteSuccessAsync_Should_Persist_Status_Json_With_Success_Flag_And_Statistics()
    {
        var world = FakeCakeWorld.CreateWindows().WithRid(Rid);
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime, world.Clock);
        var statistics = CreateStatistics(primary: 2, runtime: 5, license: 3, deployed: 4, filtered: 1);

        await repo.WriteSuccessAsync(LibraryName, statistics);

        var statusFile = ctx.Paths.GetHarvestLibraryRidStatusFile(LibraryName, Rid);
        await Assert.That(world.CakeContext.FileExists(statusFile)).IsTrue();
        var status = await world.CakeContext.ToJsonAsync<RidHarvestStatus>(statusFile);
        await Assert.That(status.Success).IsTrue();
        await Assert.That(status.LibraryName).IsEqualTo(LibraryName);
        await Assert.That(status.Rid).IsEqualTo(Rid);
        await Assert.That(status.Triplet).IsEqualTo("x64-windows-hybrid");
        await Assert.That(status.Timestamp).IsEqualTo(ClockData.Timestamp);
        await Assert.That(status.ErrorMessage).IsNull();
        await Assert.That(status.Statistics).IsNotNull();
        await Assert.That(status.Statistics!.PrimaryFilesCount).IsEqualTo(2);
        await Assert.That(status.Statistics.RuntimeFilesCount).IsEqualTo(5);
        await Assert.That(status.Statistics.LicenseFilesCount).IsEqualTo(3);
        await Assert.That(status.Statistics.DeployedPackagesCount).IsEqualTo(4);
        await Assert.That(status.Statistics.FilteredPackagesCount).IsEqualTo(1);
        await Assert.That(status.Statistics.DeploymentStrategy).IsEqualTo("DirectCopy");
    }

    [Test]
    public async Task WriteErrorAsync_Should_Persist_Status_Json_With_Failure_Flag_And_Error_Message()
    {
        var world = FakeCakeWorld.CreateWindows().WithRid(Rid);
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime, world.Clock);

        await repo.WriteErrorAsync(LibraryName, "vcpkg cache miss");

        var statusFile = ctx.Paths.GetHarvestLibraryRidStatusFile(LibraryName, Rid);
        await Assert.That(world.CakeContext.FileExists(statusFile)).IsTrue();
        var status = await world.CakeContext.ToJsonAsync<RidHarvestStatus>(statusFile);
        await Assert.That(status.Success).IsFalse();
        await Assert.That(status.ErrorMessage).IsEqualTo("vcpkg cache miss");
        await Assert.That(status.Timestamp).IsEqualTo(ClockData.Timestamp);
        await Assert.That(status.LibraryName).IsEqualTo(LibraryName);
        await Assert.That(status.Rid).IsEqualTo(Rid);
        await Assert.That(status.Statistics).IsNull();
    }

    private static DeploymentStatistics CreateStatistics(int primary, int runtime, int license, int deployed, int filtered)
    {
        var primaryList = Enumerable.Range(0, primary)
            .Select(i => new FileDeploymentInfo(new FilePath(string.Create(CultureInfo.InvariantCulture, $"primary{i}.dll")), "pkg", DeploymentLocation.FileSystem))
            .ToList();
        var runtimeList = Enumerable.Range(0, runtime)
            .Select(i => new FileDeploymentInfo(new FilePath(string.Create(CultureInfo.InvariantCulture, $"runtime{i}.dll")), "pkg", DeploymentLocation.FileSystem))
            .ToList();
        var licenseList = Enumerable.Range(0, license)
            .Select(i => new FileDeploymentInfo(new FilePath(string.Create(CultureInfo.InvariantCulture, $"copyright{i}")), "pkg", DeploymentLocation.FileSystem))
            .ToList();
        var deployedSet = new HashSet<string>(Enumerable.Range(0, deployed).Select(i => string.Create(CultureInfo.InvariantCulture, $"pkg{i}")), StringComparer.OrdinalIgnoreCase);
        var filteredSet = new HashSet<string>(
            Enumerable.Range(0, filtered).Select(i => string.Create(CultureInfo.InvariantCulture, $"filtered{i}")),
            StringComparer.OrdinalIgnoreCase);
        return new DeploymentStatistics(
            "sdl2-core",
            primaryList,
            runtimeList,
            licenseList,
            deployedSet,
            filteredSet,
            DeploymentStrategy.DirectCopy);
    }
}
