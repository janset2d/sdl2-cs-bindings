using Build.Host.Cake;
using Build.Host.Paths;
using Build.Data.Harvest;
using Build.Runtime;
using Build.Targets.Harvest.Models;
using Build.Tests.Fixtures;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Data.Harvest;

/// <summary>
/// Mock-based unit coverage for <see cref="HarvestStatusRepository"/> — argument validation,
/// cancellation propagation, and path-service interaction. File I/O round-trip behavior lives
/// in <see cref="HarvestStatusRepositoryRoundTripTests"/>; this class verifies the contract
/// that doesn't require real <see cref="Cake.Core.IO.IFileSystem"/> roundtrips.
/// </summary>
public sealed class HarvestStatusRepositoryUnitTests
{
    private const string LibraryName = "sdl2-core";
    private const string Rid = "win-x64";

    private static (HarvestStatusRepository Repo, IPathService Paths) CreateRepoWithMocks()
    {
        var ctx = Substitute.For<ICakeContext>();
        var paths = Substitute.For<IPathService>();
        var profile = Substitute.For<IRuntimeProfile>();
        profile.Rid.Returns(Rid);
        // Default path-service stubs so non-failure paths resolve. Tests that care about specific
        // paths reconfigure these.
        paths.GetHarvestLibraryRidRuntimesDir(Arg.Any<string>(), Arg.Any<string>()).Returns(new DirectoryPath("runtimes"));
        paths.GetHarvestLibraryRidStatusDir(Arg.Any<string>()).Returns(new DirectoryPath("rid-status"));
        paths.GetHarvestLibraryRidStatusFile(Arg.Any<string>(), Arg.Any<string>()).Returns(new FilePath("rid-status/win-x64.json"));
        paths.GetHarvestLibraryRidLicensesDir(Arg.Any<string>(), Arg.Any<string>()).Returns(new DirectoryPath("licenses-rid"));
        paths.GetHarvestLibraryConsolidatedLicensesDir(Arg.Any<string>()).Returns(new DirectoryPath("licenses/_consolidated"));
        paths.GetHarvestLibraryConsolidatedLicensesTempDir(Arg.Any<string>()).Returns(new DirectoryPath("licenses/_consolidated.tmp"));
        paths.GetHarvestLibraryManifestFile(Arg.Any<string>()).Returns(new FilePath("harvest-manifest.json"));
        paths.GetHarvestLibraryManifestTempFile(Arg.Any<string>()).Returns(new FilePath("harvest-manifest.tmp.json"));
        paths.GetHarvestLibrarySummaryFile(Arg.Any<string>()).Returns(new FilePath("harvest-summary.json"));
        paths.GetHarvestLibrarySummaryTempFile(Arg.Any<string>()).Returns(new FilePath("harvest-summary.tmp.json"));

        return (new HarvestStatusRepository(ctx, paths, profile), paths);
    }

    [Test]
    public async Task Constructor_Should_Throw_When_CakeContext_Is_Null()
    {
        var paths = Substitute.For<IPathService>();
        var profile = Substitute.For<IRuntimeProfile>();
        await Assert.That(() => new HarvestStatusRepository(null!, paths, profile)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_Should_Throw_When_PathService_Is_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        var profile = Substitute.For<IRuntimeProfile>();
        await Assert.That(() => new HarvestStatusRepository(ctx, null!, profile)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_Should_Throw_When_RuntimeProfile_Is_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        var paths = Substitute.For<IPathService>();
        await Assert.That(() => new HarvestStatusRepository(ctx, paths, null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Invalidate_Should_Throw_When_LibraryName_Is_Null_Or_Whitespace()
    {
        var (repo, _) = CreateRepoWithMocks();
        await Assert.That(() => repo.Invalidate(null!)).Throws<ArgumentException>();
        await Assert.That(() => repo.Invalidate("   ")).Throws<ArgumentException>();
    }

    [Test]
    public async Task Invalidate_Should_Throw_When_Cancellation_Is_Already_Requested()
    {
        var (repo, _) = CreateRepoWithMocks();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.That(() => repo.Invalidate(LibraryName, cts.Token)).Throws<OperationCanceledException>();
    }

    // Note: a path-service interaction test (Received(1).GetHarvestLibrary*) was prototyped but
    // dropped — Cake's ICakeContext extension methods (DirectoryExists/FileExists) traverse
    // FileSystem/Globber internals that NSubstitute won't auto-stub, and reaching them requires
    // wiring a Substitute graph that's effectively the FakeCakeWorld. Path-resolution contracts
    // are locked by the sociable round-trip tests below, which assert on the real file paths
    // created/deleted via FakeFileSystem — same coverage, less brittle plumbing.

    [Test]
    public async Task WriteSuccessAsync_Should_Throw_When_LibraryName_Is_Null_Or_Whitespace()
    {
        var (repo, _) = CreateRepoWithMocks();
        var stats = CreateStats();
        await Assert.That(() => repo.WriteSuccessAsync(null!, stats)).Throws<ArgumentException>();
        await Assert.That(() => repo.WriteSuccessAsync("   ", stats)).Throws<ArgumentException>();
    }

    [Test]
    public async Task WriteSuccessAsync_Should_Throw_When_Statistics_Are_Null()
    {
        var (repo, _) = CreateRepoWithMocks();
        await Assert.That(() => repo.WriteSuccessAsync(LibraryName, null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task WriteErrorAsync_Should_Throw_When_LibraryName_Is_Null_Or_Whitespace()
    {
        var (repo, _) = CreateRepoWithMocks();
        await Assert.That(() => repo.WriteErrorAsync(null!, "boom")).Throws<ArgumentException>();
        await Assert.That(() => repo.WriteErrorAsync("   ", "boom")).Throws<ArgumentException>();
    }

    [Test]
    public async Task WriteErrorAsync_Should_Throw_When_ErrorMessage_Is_Null_Or_Whitespace()
    {
        var (repo, _) = CreateRepoWithMocks();
        await Assert.That(() => repo.WriteErrorAsync(LibraryName, null!)).Throws<ArgumentException>();
        await Assert.That(() => repo.WriteErrorAsync(LibraryName, "   ")).Throws<ArgumentException>();
    }

    private static DeploymentStatistics CreateStats() =>
        new(
            "sdl2-core",
            [],
            [],
            [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            DeploymentStrategy.DirectCopy);
}

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
        var world = FakeCakeWorldV2.CreateWindows()
            .WithRid(Rid)
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/runtimes/{Rid}/native/SDL2.dll", "fake");
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime);

        repo.Invalidate(LibraryName);

        var ridDir = ctx.Paths.GetHarvestLibraryRidRuntimesDir(LibraryName, Rid);
        await Assert.That(world.CakeContext.DirectoryExists(ridDir)).IsFalse();
    }

    [Test]
    public async Task Invalidate_Should_Delete_Existing_Per_Rid_Status_File()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithRid(Rid)
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/rid-status/{Rid}.json", "{}");
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime);

        repo.Invalidate(LibraryName);

        var statusFile = ctx.Paths.GetHarvestLibraryRidStatusFile(LibraryName, Rid);
        await Assert.That(world.CakeContext.FileExists(statusFile)).IsFalse();
    }

    [Test]
    public async Task Invalidate_Should_Delete_Existing_Per_Rid_Licenses_Tree()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithRid(Rid)
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/licenses/{Rid}/zlib/copyright", "MIT");
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime);

        repo.Invalidate(LibraryName);

        var ridLicensesDir = ctx.Paths.GetHarvestLibraryRidLicensesDir(LibraryName, Rid);
        await Assert.That(world.CakeContext.DirectoryExists(ridLicensesDir)).IsFalse();
    }

    [Test]
    public async Task Invalidate_Should_Delete_Cross_Rid_Consolidated_Artifacts()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithRid(Rid)
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/licenses/_consolidated/zlib/copyright", "MIT")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/licenses/_consolidated.tmp/zlib/copyright", "MIT")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/harvest-manifest.json", "{}")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/harvest-manifest.tmp.json", "{}")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/harvest-summary.json", "{}")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/harvest-summary.tmp.json", "{}");
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime);

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
        var world = FakeCakeWorldV2.CreateWindows().WithRid(Rid);
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime);

        repo.Invalidate(LibraryName);

        await Assert.That(world.CakeContext.DirectoryExists(ctx.Paths.GetHarvestLibraryRidRuntimesDir(LibraryName, Rid))).IsFalse();
    }

    [Test]
    public async Task WriteSuccessAsync_Should_Persist_Status_Json_With_Success_Flag_And_Statistics()
    {
        var world = FakeCakeWorldV2.CreateWindows().WithRid(Rid);
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime);
        var statistics = CreateStatistics(primary: 2, runtime: 5, license: 3, deployed: 4, filtered: 1);

        await repo.WriteSuccessAsync(LibraryName, statistics);

        var statusFile = ctx.Paths.GetHarvestLibraryRidStatusFile(LibraryName, Rid);
        await Assert.That(world.CakeContext.FileExists(statusFile)).IsTrue();
        var status = await world.CakeContext.ToJsonAsync<RidHarvestStatus>(statusFile);
        await Assert.That(status.Success).IsTrue();
        await Assert.That(status.LibraryName).IsEqualTo(LibraryName);
        await Assert.That(status.Rid).IsEqualTo(Rid);
        await Assert.That(status.Triplet).IsEqualTo("x64-windows-hybrid");
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
        var world = FakeCakeWorldV2.CreateWindows().WithRid(Rid);
        var ctx = world.CreateBuildContext();
        var repo = new HarvestStatusRepository(world.CakeContext, ctx.Paths, ctx.Runtime);

        await repo.WriteErrorAsync(LibraryName, "vcpkg cache miss");

        var statusFile = ctx.Paths.GetHarvestLibraryRidStatusFile(LibraryName, Rid);
        await Assert.That(world.CakeContext.FileExists(statusFile)).IsTrue();
        var status = await world.CakeContext.ToJsonAsync<RidHarvestStatus>(statusFile);
        await Assert.That(status.Success).IsFalse();
        await Assert.That(status.ErrorMessage).IsEqualTo("vcpkg cache miss");
        await Assert.That(status.LibraryName).IsEqualTo(LibraryName);
        await Assert.That(status.Rid).IsEqualTo(Rid);
        await Assert.That(status.Statistics).IsNull();
    }

    private static DeploymentStatistics CreateStatistics(int primary, int runtime, int license, int deployed, int filtered)
    {
        var primaryList = Enumerable.Range(0, primary)
            .Select(i => new FileDeploymentInfo(new FilePath($"primary{i}.dll"), "pkg", DeploymentLocation.FileSystem))
            .ToList();
        var runtimeList = Enumerable.Range(0, runtime)
            .Select(i => new FileDeploymentInfo(new FilePath($"runtime{i}.dll"), "pkg", DeploymentLocation.FileSystem))
            .ToList();
        var licenseList = Enumerable.Range(0, license)
            .Select(i => new FileDeploymentInfo(new FilePath($"copyright{i}"), "pkg", DeploymentLocation.FileSystem))
            .ToList();
        var deployedSet = new HashSet<string>(Enumerable.Range(0, deployed).Select(i => $"pkg{i}"), StringComparer.OrdinalIgnoreCase);
        var filteredSet = new HashSet<string>(Enumerable.Range(0, filtered).Select(i => $"filtered{i}"), StringComparer.OrdinalIgnoreCase);
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
