using System.Text.Json;
using Build.Modules.Harvesting.Models;
using Build.Tests.Fixtures;
using Build.Tasks.Harvest;

namespace Build.Tests.Unit.Tasks.Harvest;

public class ConsolidateHarvestTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Test]
    public async Task RunAsync_Should_Calculate_Correct_Summary_For_Mixed_Rids()
    {
        var manifest = await RunConsolidationForStatusesAsync(
            "SDL2",
            CreateSuccessStatus("SDL2", "win-x64", "x64-windows-hybrid"),
            CreateSuccessStatus("SDL2", "linux-x64", "x64-linux-hybrid"),
            CreateFailedStatus("SDL2", "osx-arm64", "arm64-osx-dynamic", "Build timeout"));

        await Assert.That(manifest.LibraryName).IsEqualTo("SDL2");
        await Assert.That(manifest.Summary.TotalRids).IsEqualTo(3);
        await Assert.That(manifest.Summary.SuccessfulRids).IsEqualTo(2);
        await Assert.That(manifest.Summary.FailedRids).IsEqualTo(1);
    }

    [Test]
    public async Task RunAsync_Should_Calculate_Correct_Success_Rate_For_All_Success_Rids()
    {
        var manifest = await RunConsolidationForStatusesAsync(
            "SDL2_image",
            CreateSuccessStatus("SDL2_image", "win-x64", "x64-windows-hybrid"),
            CreateSuccessStatus("SDL2_image", "linux-x64", "x64-linux-hybrid"),
            CreateSuccessStatus("SDL2_image", "osx-x64", "x64-osx-hybrid"));

        await Assert.That(manifest.Summary.SuccessRate).IsEqualTo(1.0);
        await Assert.That(manifest.Summary.SuccessfulRids).IsEqualTo(3);
        await Assert.That(manifest.Summary.FailedRids).IsEqualTo(0);
    }

    [Test]
    public async Task RunAsync_Should_Calculate_Correct_Summary_For_All_Failed_Rids()
    {
        var manifest = await RunConsolidationForStatusesAsync(
            "SDL2_mixer",
            CreateFailedStatus("SDL2_mixer", "win-x64", "x64-windows-hybrid", "vcpkg install failed"),
            CreateFailedStatus("SDL2_mixer", "linux-x64", "x64-linux-hybrid", "ldd not found"));

        await Assert.That(manifest.Summary.SuccessRate).IsEqualTo(0.0);
        await Assert.That(manifest.Summary.SuccessfulRids).IsEqualTo(0);
        await Assert.That(manifest.Summary.FailedRids).IsEqualTo(2);
    }

    [Test]
    public async Task RidHarvestStatus_Should_Serialize_And_Deserialize_Correctly()
    {
        var original = CreateSuccessStatus("SDL2", "win-x64", "x64-windows-hybrid");
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<RidHarvestStatus>(json, JsonOptions);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.LibraryName).IsEqualTo("SDL2");
        await Assert.That(deserialized.Rid).IsEqualTo("win-x64");
        await Assert.That(deserialized.Triplet).IsEqualTo("x64-windows-hybrid");
        await Assert.That(deserialized.Success).IsTrue();
        await Assert.That(deserialized.ErrorMessage).IsNull();
    }

    [Test]
    public async Task RidHarvestStatus_Should_Preserve_Error_Message_On_Failure()
    {
        var original = CreateFailedStatus("SDL2", "linux-arm64", "arm64-linux-dynamic", "Package not found");
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<RidHarvestStatus>(json, JsonOptions);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Success).IsFalse();
        await Assert.That(deserialized.ErrorMessage).IsEqualTo("Package not found");
        await Assert.That(deserialized.Statistics).IsNull();
    }

    [Test]
    public async Task HarvestManifest_Should_Serialize_And_Deserialize_Roundtrip()
    {
        var statuses = new List<RidHarvestStatus>
        {
            CreateSuccessStatus("SDL2", "win-x64", "x64-windows-hybrid"),
            CreateFailedStatus("SDL2", "osx-arm64", "arm64-osx-dynamic", "timeout"),
        };

        var manifest = new HarvestManifest
        {
            LibraryName = "SDL2",
            GeneratedTimestamp = DateTimeOffset.UtcNow,
            Rids = statuses.AsReadOnly(),
            Summary = new HarvestSummary
            {
                TotalRids = 2,
                SuccessfulRids = 1,
                FailedRids = 1,
                SuccessRate = 0.5,
            },
        };

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<HarvestManifest>(json, JsonOptions);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.LibraryName).IsEqualTo("SDL2");
        await Assert.That(deserialized.Rids.Count).IsEqualTo(2);
        await Assert.That(deserialized.Summary.TotalRids).IsEqualTo(2);
    }

    [Test]
    public async Task RunAsync_Should_Generate_Harvest_Manifest_And_Summary_From_Rid_Status_Files()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithHarvestStatus("SDL2", "win-x64", CreateSuccessStatus("SDL2", "win-x64", "x64-windows-hybrid"))
            .WithHarvestStatus("SDL2", "linux-x64", CreateFailedStatus("SDL2", "linux-x64", "x64-linux-hybrid", "ldd failed"))
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask();

        await task.RunAsync(repo.BuildContext);

        const string manifestPath = "artifacts/harvest_output/SDL2/harvest-manifest.json";
        const string summaryPath = "artifacts/harvest_output/SDL2/harvest-summary.json";

        await Assert.That(repo.Exists(manifestPath)).IsTrue();
        await Assert.That(repo.Exists(summaryPath)).IsTrue();

        var manifestJson = await repo.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<HarvestManifest>(manifestJson, JsonOptions);

        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest!.LibraryName).IsEqualTo("SDL2");
        await Assert.That(manifest.Rids.Count).IsEqualTo(2);
        await Assert.That(manifest.Summary.TotalRids).IsEqualTo(2);
        await Assert.That(manifest.Summary.SuccessfulRids).IsEqualTo(1);
        await Assert.That(manifest.Summary.FailedRids).IsEqualTo(1);
        await Assert.That(manifest.Summary.SuccessRate).IsEqualTo(0.5);
    }

    [Test]
    public async Task RunAsync_Should_Ignore_Invalid_Rid_Status_Files_When_At_Least_One_Valid_File_Exists()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithHarvestStatus("SDL2_image", "win-x64", CreateSuccessStatus("SDL2_image", "win-x64", "x64-windows-hybrid"))
            .WithTextFile("artifacts/harvest_output/SDL2_image/rid-status/corrupt.json", "{ this is not valid json")
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask();

        await task.RunAsync(repo.BuildContext);

        const string manifestPath = "artifacts/harvest_output/SDL2_image/harvest-manifest.json";
        await Assert.That(repo.Exists(manifestPath)).IsTrue();

        var manifestJson = await repo.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<HarvestManifest>(manifestJson, JsonOptions);

        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest!.Rids.Count).IsEqualTo(1);
        await Assert.That(manifest.Summary.TotalRids).IsEqualTo(1);
        await Assert.That(manifest.Summary.SuccessRate).IsEqualTo(1.0);
    }

    [Test]
    public async Task RunAsync_Should_Not_Generate_Manifest_When_All_Rid_Status_Files_Are_Invalid()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithTextFile("artifacts/harvest_output/SDL2_mixer/rid-status/one.json", "{ invalid")
            .WithTextFile("artifacts/harvest_output/SDL2_mixer/rid-status/two.json", "also invalid")
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask();

        await task.RunAsync(repo.BuildContext);

        const string manifestPath = "artifacts/harvest_output/SDL2_mixer/harvest-manifest.json";
        const string summaryPath = "artifacts/harvest_output/SDL2_mixer/harvest-summary.json";

        await Assert.That(repo.Exists(manifestPath)).IsFalse();
        await Assert.That(repo.Exists(summaryPath)).IsFalse();
    }

    private static RidHarvestStatus CreateSuccessStatus(string library, string rid, string triplet) => new()
    {
        LibraryName = library,
        Rid = rid,
        Triplet = triplet,
        Success = true,
        ErrorMessage = null,
        Timestamp = DateTimeOffset.UtcNow,
        Statistics = new HarvestStatistics
        {
            PrimaryFilesCount = 1,
            RuntimeFilesCount = 2,
            LicenseFilesCount = 1,
            DeployedPackagesCount = 3,
            FilteredPackagesCount = 0,
            DeploymentStrategy = "DirectCopy",
        },
    };

    private static RidHarvestStatus CreateFailedStatus(string library, string rid, string triplet, string error) => new()
    {
        LibraryName = library,
        Rid = rid,
        Triplet = triplet,
        Success = false,
        ErrorMessage = error,
        Timestamp = DateTimeOffset.UtcNow,
        Statistics = null,
    };

    private static async Task<HarvestManifest> RunConsolidationForStatusesAsync(string libraryName, params RidHarvestStatus[] statuses)
    {
        var builder = new FakeRepoBuilder(FakeRepoPlatform.Windows);

        foreach (var status in statuses)
        {
            builder.WithHarvestStatus(libraryName, status.Rid, status);
        }

        var repo = builder.BuildContextWithHandles();

        var task = new ConsolidateHarvestTask();
        await task.RunAsync(repo.BuildContext);

        var manifestPath = $"artifacts/harvest_output/{libraryName}/harvest-manifest.json";
        var manifestJson = await repo.ReadAllTextAsync(manifestPath);
        return JsonSerializer.Deserialize<HarvestManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize generated harvest manifest.");
    }
}
