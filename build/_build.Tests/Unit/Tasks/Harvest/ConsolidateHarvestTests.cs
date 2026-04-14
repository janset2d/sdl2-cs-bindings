using System.Text.Json;
using Build.Models;
using Build.Tests.Fixtures;
using Build.Tasks.Harvest;
using Cake.Core.IO;
using IOPath = System.IO.Path;

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
        var harvestRoot = CreateTempHarvestOutputRoot();
        try
        {
            await WriteRidStatusFileAsync(
                harvestRoot,
                "SDL2",
                "win-x64.json",
                CreateSuccessStatus("SDL2", "win-x64", "x64-windows-hybrid"));

            await WriteRidStatusFileAsync(
                harvestRoot,
                "SDL2",
                "linux-x64.json",
                CreateFailedStatus("SDL2", "linux-x64", "x64-linux-hybrid", "ldd failed"));

            var task = new ConsolidateHarvestTask();
            var context = TaskTestHelpers.CreateBuildContext(new DirectoryPath(harvestRoot));

            await task.RunAsync(context);

            var manifestPath = IOPath.Combine(harvestRoot, "SDL2", "harvest-manifest.json");
            var summaryPath = IOPath.Combine(harvestRoot, "SDL2", "harvest-summary.json");

            await Assert.That(File.Exists(manifestPath)).IsTrue();
            await Assert.That(File.Exists(summaryPath)).IsTrue();

            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<HarvestManifest>(manifestJson, JsonOptions);

            await Assert.That(manifest).IsNotNull();
            await Assert.That(manifest!.LibraryName).IsEqualTo("SDL2");
            await Assert.That(manifest.Rids.Count).IsEqualTo(2);
            await Assert.That(manifest.Summary.TotalRids).IsEqualTo(2);
            await Assert.That(manifest.Summary.SuccessfulRids).IsEqualTo(1);
            await Assert.That(manifest.Summary.FailedRids).IsEqualTo(1);
            await Assert.That(manifest.Summary.SuccessRate).IsEqualTo(0.5);
        }
        finally
        {
            TaskTestHelpers.DeleteDirectoryQuietly(harvestRoot);
        }
    }

    [Test]
    public async Task RunAsync_Should_Ignore_Invalid_Rid_Status_Files_When_At_Least_One_Valid_File_Exists()
    {
        var harvestRoot = CreateTempHarvestOutputRoot();
        try
        {
            await WriteRidStatusFileAsync(
                harvestRoot,
                "SDL2_image",
                "win-x64.json",
                CreateSuccessStatus("SDL2_image", "win-x64", "x64-windows-hybrid"));

            var ridStatusDir = IOPath.Combine(harvestRoot, "SDL2_image", "rid-status");
            await File.WriteAllTextAsync(IOPath.Combine(ridStatusDir, "corrupt.json"), "{ this is not valid json");

            var task = new ConsolidateHarvestTask();
            var context = TaskTestHelpers.CreateBuildContext(new DirectoryPath(harvestRoot));

            await task.RunAsync(context);

            var manifestPath = IOPath.Combine(harvestRoot, "SDL2_image", "harvest-manifest.json");
            await Assert.That(File.Exists(manifestPath)).IsTrue();

            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<HarvestManifest>(manifestJson, JsonOptions);

            await Assert.That(manifest).IsNotNull();
            await Assert.That(manifest!.Rids.Count).IsEqualTo(1);
            await Assert.That(manifest.Summary.TotalRids).IsEqualTo(1);
            await Assert.That(manifest.Summary.SuccessRate).IsEqualTo(1.0);
        }
        finally
        {
            TaskTestHelpers.DeleteDirectoryQuietly(harvestRoot);
        }
    }

    [Test]
    public async Task RunAsync_Should_Not_Generate_Manifest_When_All_Rid_Status_Files_Are_Invalid()
    {
        var harvestRoot = CreateTempHarvestOutputRoot();
        try
        {
            var ridStatusDir = IOPath.Combine(harvestRoot, "SDL2_mixer", "rid-status");
            Directory.CreateDirectory(ridStatusDir);

            await File.WriteAllTextAsync(IOPath.Combine(ridStatusDir, "one.json"), "{ invalid");
            await File.WriteAllTextAsync(IOPath.Combine(ridStatusDir, "two.json"), "also invalid");

            var task = new ConsolidateHarvestTask();
            var context = TaskTestHelpers.CreateBuildContext(new DirectoryPath(harvestRoot));

            await task.RunAsync(context);

            var manifestPath = IOPath.Combine(harvestRoot, "SDL2_mixer", "harvest-manifest.json");
            var summaryPath = IOPath.Combine(harvestRoot, "SDL2_mixer", "harvest-summary.json");

            await Assert.That(File.Exists(manifestPath)).IsFalse();
            await Assert.That(File.Exists(summaryPath)).IsFalse();
        }
        finally
        {
            TaskTestHelpers.DeleteDirectoryQuietly(harvestRoot);
        }
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
        var harvestRoot = CreateTempHarvestOutputRoot();
        try
        {
            foreach (var status in statuses)
            {
                await WriteRidStatusFileAsync(harvestRoot, libraryName, $"{status.Rid}.json", status);
            }

            var task = new ConsolidateHarvestTask();
            var context = TaskTestHelpers.CreateBuildContext(new DirectoryPath(harvestRoot));
            await task.RunAsync(context);

            var manifestPath = IOPath.Combine(harvestRoot, libraryName, "harvest-manifest.json");
            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            return JsonSerializer.Deserialize<HarvestManifest>(manifestJson, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize generated harvest manifest.");
        }
        finally
        {
            TaskTestHelpers.DeleteDirectoryQuietly(harvestRoot);
        }
    }

    private static string CreateTempHarvestOutputRoot()
    {
        var path = IOPath.Combine(IOPath.GetTempPath(), "sdl2-bindings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WriteRidStatusFileAsync(string harvestRoot, string libraryName, string fileName, RidHarvestStatus ridStatus)
    {
        var ridStatusDir = IOPath.Combine(harvestRoot, libraryName, "rid-status");
        Directory.CreateDirectory(ridStatusDir);

        var filePath = IOPath.Combine(ridStatusDir, fileName);
        var json = JsonSerializer.Serialize(ridStatus, JsonOptions);

        await File.WriteAllTextAsync(filePath, json);
    }

}
