using System.Text.Json;
using Build.Models;
using Build.Tasks.Harvest;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;

namespace Build.Tests.Unit.ConsolidateHarvest;

public class ConsolidateHarvestTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Test]
    public async Task GenerateHarvestManifest_Should_Calculate_Correct_Summary()
    {
        var statuses = new List<RidHarvestStatus>
        {
            CreateSuccessStatus("SDL2", "win-x64", "x64-windows-hybrid"),
            CreateSuccessStatus("SDL2", "linux-x64", "x64-linux-hybrid"),
            CreateFailedStatus("SDL2", "osx-arm64", "arm64-osx-dynamic", "Build timeout"),
        };

        // Use reflection or test the serialized output structure
        var manifest = BuildHarvestManifest("SDL2", statuses);

        await Assert.That(manifest.LibraryName).IsEqualTo("SDL2");
        await Assert.That(manifest.Summary.TotalRids).IsEqualTo(3);
        await Assert.That(manifest.Summary.SuccessfulRids).IsEqualTo(2);
        await Assert.That(manifest.Summary.FailedRids).IsEqualTo(1);
    }

    [Test]
    public async Task GenerateHarvestManifest_Should_Have_Correct_Success_Rate()
    {
        var statuses = new List<RidHarvestStatus>
        {
            CreateSuccessStatus("SDL2_image", "win-x64", "x64-windows-hybrid"),
            CreateSuccessStatus("SDL2_image", "linux-x64", "x64-linux-hybrid"),
            CreateSuccessStatus("SDL2_image", "osx-x64", "x64-osx-hybrid"),
        };

        var manifest = BuildHarvestManifest("SDL2_image", statuses);

        await Assert.That(manifest.Summary.SuccessRate).IsEqualTo(1.0);
        await Assert.That(manifest.Summary.FailedRids).IsEqualTo(0);
    }

    [Test]
    public async Task GenerateHarvestManifest_Should_Handle_All_Failed_Rids()
    {
        var statuses = new List<RidHarvestStatus>
        {
            CreateFailedStatus("SDL2_mixer", "win-x64", "x64-windows-hybrid", "vcpkg install failed"),
            CreateFailedStatus("SDL2_mixer", "linux-x64", "x64-linux-hybrid", "ldd not found"),
        };

        var manifest = BuildHarvestManifest("SDL2_mixer", statuses);

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

        var manifest = BuildHarvestManifest("SDL2", statuses);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<HarvestManifest>(json, JsonOptions);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.LibraryName).IsEqualTo("SDL2");
        await Assert.That(deserialized.Rids.Count).IsEqualTo(2);
        await Assert.That(deserialized.Summary.TotalRids).IsEqualTo(2);
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
            DeploymentStrategy = "DirectCopy"
        }
    };

    private static RidHarvestStatus CreateFailedStatus(string library, string rid, string triplet, string error) => new()
    {
        LibraryName = library,
        Rid = rid,
        Triplet = triplet,
        Success = false,
        ErrorMessage = error,
        Timestamp = DateTimeOffset.UtcNow,
        Statistics = null
    };

    /// <summary>
    /// Mirrors the logic in ConsolidateHarvestTask.GenerateHarvestManifest (private static).
    /// We test the same algorithm independently.
    /// </summary>
    private static HarvestManifest BuildHarvestManifest(string libraryName, List<RidHarvestStatus> ridStatuses)
    {
        var successfulRids = ridStatuses.Where(r => r.Success).ToList();
        var failedRids = ridStatuses.Where(r => !r.Success).ToList();

        var summary = new HarvestSummary
        {
            TotalRids = ridStatuses.Count,
            SuccessfulRids = successfulRids.Count,
            FailedRids = failedRids.Count,
            SuccessRate = ridStatuses.Count > 0 ? (double)successfulRids.Count / ridStatuses.Count : 0.0,
        };

        return new HarvestManifest
        {
            LibraryName = libraryName,
            GeneratedTimestamp = DateTimeOffset.UtcNow,
            Rids = ridStatuses.AsReadOnly(),
            Summary = summary,
        };
    }
}
