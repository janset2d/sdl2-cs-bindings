using System.Text.Json;
using Build.Domain.Harvesting;
using Build.Domain.Harvesting.Models;

namespace Build.Tests.Fixtures.Seeders;

/// <summary>
/// Writes a single <c>artifacts/harvest_output/{library}/rid-status/{rid}.json</c> file using
/// the same <see cref="HarvestJsonContract.Options"/> that <c>HarvestTask</c> writes in
/// production. Use the factory methods (<see cref="Success"/>, <see cref="Failure"/>) for the
/// common cases; use the constructor directly when a test needs a hand-shaped status record.
/// </summary>
public sealed class HarvestStatusSeeder : IFixtureSeeder
{
    private readonly RidHarvestStatus _status;

    public HarvestStatusSeeder(RidHarvestStatus status)
    {
        _status = status ?? throw new ArgumentNullException(nameof(status));
    }

    public string LibraryName => _status.LibraryName;

    public string Rid => _status.Rid;

    public void Apply(FakeRepoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var json = JsonSerializer.Serialize(_status, HarvestJsonContract.Options);
        builder.WithTextFile($"artifacts/harvest_output/{_status.LibraryName}/rid-status/{_status.Rid}.json", json);
    }

    public static HarvestStatusSeeder Success(
        string libraryName,
        string rid,
        string triplet,
        HarvestStatistics? statistics = null,
        DateTimeOffset? timestamp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);

        var status = new RidHarvestStatus
        {
            LibraryName = libraryName,
            Rid = rid,
            Triplet = triplet,
            Success = true,
            ErrorMessage = null,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            Statistics = statistics ?? DefaultStatistics(),
        };

        return new HarvestStatusSeeder(status);
    }

    public static HarvestStatusSeeder Failure(
        string libraryName,
        string rid,
        string triplet,
        string errorMessage,
        DateTimeOffset? timestamp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        var status = new RidHarvestStatus
        {
            LibraryName = libraryName,
            Rid = rid,
            Triplet = triplet,
            Success = false,
            ErrorMessage = errorMessage,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            Statistics = null,
        };

        return new HarvestStatusSeeder(status);
    }

    private static HarvestStatistics DefaultStatistics()
    {
        return new HarvestStatistics
        {
            PrimaryFilesCount = 1,
            RuntimeFilesCount = 0,
            LicenseFilesCount = 0,
            DeployedPackagesCount = 1,
            FilteredPackagesCount = 0,
            DeploymentStrategy = "DirectCopy",
        };
    }
}
