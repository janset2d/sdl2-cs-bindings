using System.Globalization;
using Build.Data.Harvest;
using Build.Host.Cake;
using Build.Host.Paths;
using Build.Tests.Fixtures;
using Cake.Common.IO;
using Cake.Core;
using NSubstitute;

namespace Build.Tests.Unit.Data.Harvest;

/// <summary>
/// Mock-based unit coverage for <see cref="HarvestManifestRepository"/> argument validation.
/// File IO behavior lives in <see cref="HarvestManifestRepositoryRoundTripTests"/>.
/// </summary>
public sealed class HarvestManifestRepositoryUnitTests
{
    private const string LibraryName = "sdl2-core";

    [Test]
    public async Task Constructor_Should_Throw_When_CakeContext_Is_Null()
    {
        var paths = Substitute.For<IPathService>();

        await Assert.That(() => new HarvestManifestRepository(null!, paths)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_Should_Throw_When_PathService_Is_Null()
    {
        var context = Substitute.For<ICakeContext>();

        await Assert.That(() => new HarvestManifestRepository(context, null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task LoadRidStatusesAsync_Should_Throw_When_LibraryName_Is_Null_Or_Whitespace()
    {
        var repository = CreateRepository();

        await Assert.That(() => repository.LoadRidStatusesAsync(null!)).Throws<ArgumentException>();
        await Assert.That(() => repository.LoadRidStatusesAsync("   ")).Throws<ArgumentException>();
    }

    [Test]
    public async Task LoadRidStatusesAsync_Should_Throw_When_Cancellation_Is_Already_Requested()
    {
        var repository = CreateRepository();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.That(() => repository.LoadRidStatusesAsync(LibraryName, cts.Token)).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task LoadManifestAsync_Should_Throw_When_LibraryName_Is_Null_Or_Whitespace()
    {
        var repository = CreateRepository();

        await Assert.That(async () => await repository.LoadManifestAsync(null!)).Throws<ArgumentException>();
        await Assert.That(async () => await repository.LoadManifestAsync("   ")).Throws<ArgumentException>();
    }

    [Test]
    public async Task WriteManifestTempAsync_Should_Throw_When_Manifest_Is_Null()
    {
        var repository = CreateRepository();

        await Assert.That(() => repository.WriteManifestTempAsync(LibraryName, null!)).Throws<ArgumentNullException>();
    }

    private static HarvestManifestRepository CreateRepository()
    {
        var world = FakeCakeWorld.CreateWindows();
        var context = world.CreateBuildContext();
        return new HarvestManifestRepository(world.CakeContext, context.Paths);
    }
}

/// <summary>
/// Sociable round-trip coverage for <see cref="HarvestManifestRepository"/> using Cake
/// FakeFileSystem and embedded Harvest JSON fixtures.
/// </summary>
public sealed class HarvestManifestRepositoryRoundTripTests
{
    private const string LibraryName = "sdl2-core";

    [Test]
    public async Task LoadRidStatusesAsync_Should_Return_Null_When_Rid_Status_Directory_Is_Missing()
    {
        var (repository, _) = CreateRepository(FakeCakeWorld.CreateWindows());

        var statuses = await repository.LoadRidStatusesAsync(LibraryName);

        await Assert.That(statuses).IsNull();
    }

    [Test]
    public async Task LoadRidStatusesAsync_Should_Return_Null_When_Rid_Status_Directory_Has_No_Json_Files()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/rid-status/.placeholder", string.Empty);
        var (repository, _) = CreateRepository(world);

        var statuses = await repository.LoadRidStatusesAsync(LibraryName);

        await Assert.That(statuses).IsNull();
    }

    [Test]
    public async Task LoadRidStatusesAsync_Should_Return_Null_When_All_Rid_Status_Files_Parse_To_Null()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithTextFile(
                $"artifacts/harvest_output/{LibraryName}/rid-status/win-x64.json",
                FixtureLoader.Load("Harvest/rid-status-null.json"));
        var (repository, _) = CreateRepository(world);

        var statuses = await repository.LoadRidStatusesAsync(LibraryName);

        await Assert.That(statuses).IsNull();
    }

    [Test]
    public async Task LoadRidStatusesAsync_Should_Throw_CakeException_When_Rid_Status_File_Is_Invalid_Json()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithTextFile(
                $"artifacts/harvest_output/{LibraryName}/rid-status/win-x64.json",
                FixtureLoader.Load("Harvest/rid-status-invalid.json"));
        var (repository, _) = CreateRepository(world);

        var exception = await Assert.That(() => repository.LoadRidStatusesAsync(LibraryName)).Throws<CakeException>();

        await Assert.That(exception!.Message).Contains("unreadable or invalid", StringComparison.Ordinal);
        await Assert.That(exception.Message).Contains("win-x64.json", StringComparison.Ordinal);
    }

    [Test]
    public async Task LoadRidStatusesAsync_Should_Load_All_Rid_Status_Files()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithTextFile(
                $"artifacts/harvest_output/{LibraryName}/rid-status/win-x64.json",
                FixtureLoader.Load("Harvest/rid-status-success-win-x64.json"))
            .WithTextFile(
                $"artifacts/harvest_output/{LibraryName}/rid-status/linux-x64.json",
                FixtureLoader.Load("Harvest/rid-status-success-linux-x64.json"));
        var (repository, _) = CreateRepository(world);

        var statuses = await repository.LoadRidStatusesAsync(LibraryName);

        await Assert.That(statuses).IsNotNull();
        await Assert.That(statuses!.Count).IsEqualTo(2);
        var byRid = statuses.ToDictionary(status => status.Rid, StringComparer.OrdinalIgnoreCase);
        await Assert.That(byRid["win-x64"].Triplet).IsEqualTo("x64-windows-hybrid");
        await Assert.That(byRid["linux-x64"].Statistics!.PrimaryFilesCount).IsEqualTo(2);
    }

    [Test]
    public async Task LoadManifestAsync_Should_Load_Harvest_Manifest_File()
    {
        const string manifestLibrary = "sdl2";
        var world = FakeCakeWorld.CreateWindows()
            .WithTextFile(
                $"artifacts/harvest_output/{manifestLibrary}/harvest-manifest.json",
                FixtureLoader.Load("Harvest/harvest-manifest-ready.json"));
        var (repository, _) = CreateRepository(world);

        var manifest = await repository.LoadManifestAsync(manifestLibrary);

        await Assert.That(manifest.LibraryName).IsEqualTo(manifestLibrary);
        await Assert.That(manifest.Summary.SuccessfulRids).IsEqualTo(3);
        await Assert.That(manifest.Consolidation!.LicenseEntriesCount).IsEqualTo(3);
    }

    [Test]
    public async Task WriteManifestTempAsync_Should_Write_Manifest_And_Summary_With_Json_Parity()
    {
        var world = FakeCakeWorld.CreateWindows();
        var (repository, paths) = CreateRepository(world);
        var manifest = CreateManifest();

        await repository.WriteManifestTempAsync(LibraryName, manifest);

        var manifestTemp = paths.GetHarvestLibraryManifestTempFile(LibraryName);
        var summaryTemp = paths.GetHarvestLibrarySummaryTempFile(LibraryName);
        await Assert.That(world.CakeContext.FileExists(manifestTemp)).IsTrue();
        await Assert.That(world.CakeContext.FileExists(summaryTemp)).IsTrue();
        await Assert.That(await world.CakeContext.ReadAllTextAsync(manifestTemp))
            .IsEqualTo(world.CakeContext.SerializeJson(manifest));
        await Assert.That(await world.CakeContext.ReadAllTextAsync(summaryTemp))
            .IsEqualTo(world.CakeContext.SerializeJson(manifest.Summary));
    }

    private static (HarvestManifestRepository Repository, IPathService Paths) CreateRepository(FakeCakeWorld world)
    {
        var context = world.CreateBuildContext();
        return (new HarvestManifestRepository(world.CakeContext, context.Paths), context.Paths);
    }

    private static HarvestManifest CreateManifest()
    {
        var ridStatus = new RidHarvestStatus
        {
            LibraryName = LibraryName,
            Rid = "win-x64",
            Triplet = "x64-windows-hybrid",
            Success = true,
            ErrorMessage = null,
            Timestamp = DateTimeOffset.Parse("2026-05-09T09:55:00+00:00", CultureInfo.InvariantCulture),
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

        return new HarvestManifest
        {
            LibraryName = LibraryName,
            GeneratedTimestamp = DateTimeOffset.Parse("2026-05-09T10:00:00+00:00", CultureInfo.InvariantCulture),
            Rids = [ridStatus],
            Summary = new HarvestSummary
            {
                TotalRids = 1,
                SuccessfulRids = 1,
                FailedRids = 0,
                SuccessRate = 1.0,
            },
            Consolidation = new ConsolidationState
            {
                LicensesConsolidated = true,
                LicenseEntriesCount = 1,
                DivergentLicenses = [],
            },
        };
    }
}
