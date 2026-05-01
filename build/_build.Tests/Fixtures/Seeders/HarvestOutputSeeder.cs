using System.Text.Json;
using Build.Domain.Harvesting;
using Build.Domain.Harvesting.Models;

namespace Build.Tests.Fixtures.Seeders;

/// <summary>
/// Writes a coherent per-library / per-RID harvest output subset under
/// <c>artifacts/harvest_output/{library}/</c>. A single seeder instance describes one
/// (library, rid) pair; compose multiple seeders to materialize multi-RID or multi-library
/// fixtures.
/// <para>
/// Produces:
/// <list type="bullet">
///   <item><description><c>runtimes/{rid}/native/{primary files}</c></description></item>
///   <item><description><c>licenses/{rid}/{package}/{license file}</c> (RID-scoped by default — matches the post-H1 layout; set <see cref="LicenseLayout"/> to <see cref="HarvestLicenseLayout.LibraryFlat"/> for legacy tests)</description></item>
///   <item><description><c>rid-status/{rid}.json</c> (via embedded <see cref="HarvestStatusSeeder"/>)</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class HarvestOutputSeeder : IFixtureSeeder
{
    private readonly string _library;
    private readonly string _rid;
    private readonly string _triplet;
    private readonly List<HarvestPrimaryFile> _primaries = new();
    private readonly List<HarvestLicenseFile> _licenses = new();
    private bool _success = true;
    private string? _errorMessage;
    private DateTimeOffset? _timestamp;

    public HarvestOutputSeeder(string library, string rid, string triplet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(library);
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);

        _library = library;
        _rid = rid;
        _triplet = triplet;
    }

    public HarvestLicenseLayout LicenseLayout { get; init; } = HarvestLicenseLayout.PerRid;

    public HarvestOutputSeeder WithPrimary(string fileName, string content = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _primaries.Add(new HarvestPrimaryFile(fileName, content));
        return this;
    }

    public HarvestOutputSeeder WithLicense(string package, string fileName = "copyright", string content = "MIT")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _licenses.Add(new HarvestLicenseFile(package, fileName, content));
        return this;
    }

    public HarvestOutputSeeder AsSuccess()
    {
        _success = true;
        _errorMessage = null;
        return this;
    }

    public HarvestOutputSeeder AsFailure(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        _success = false;
        _errorMessage = errorMessage;
        return this;
    }

    public HarvestOutputSeeder AtTimestamp(DateTimeOffset timestamp)
    {
        _timestamp = timestamp;
        return this;
    }

    public void Apply(FakeRepoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var primary in _primaries)
        {
            builder.WithTextFile($"artifacts/harvest_output/{_library}/runtimes/{_rid}/native/{primary.FileName}", primary.Content);
        }

        foreach (var license in _licenses)
        {
            var path = LicenseLayout == HarvestLicenseLayout.PerRid
                ? $"artifacts/harvest_output/{_library}/licenses/{_rid}/{license.Package}/{license.FileName}"
                : $"artifacts/harvest_output/{_library}/licenses/{license.Package}/{license.FileName}";
            builder.WithTextFile(path, license.Content);
        }

        var status = new RidHarvestStatus
        {
            LibraryName = _library,
            Rid = _rid,
            Triplet = _triplet,
            Success = _success,
            ErrorMessage = _errorMessage,
            Timestamp = _timestamp ?? DateTimeOffset.UtcNow,
            Statistics = _success ? ComputeStatistics() : null,
        };

        var json = JsonSerializer.Serialize(status, HarvestJsonContract.Options);
        builder.WithTextFile($"artifacts/harvest_output/{_library}/rid-status/{_rid}.json", json);
    }

    private HarvestStatistics ComputeStatistics()
    {
        return new HarvestStatistics
        {
            PrimaryFilesCount = _primaries.Count,
            RuntimeFilesCount = 0,
            LicenseFilesCount = _licenses.Count,
            DeployedPackagesCount = _licenses.Select(l => l.Package).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            FilteredPackagesCount = 0,
            DeploymentStrategy = "DirectCopy",
        };
    }

    private sealed record HarvestPrimaryFile(string FileName, string Content);

    private sealed record HarvestLicenseFile(string Package, string FileName, string Content);
}

public enum HarvestLicenseLayout
{
    /// <summary>Licenses written under <c>licenses/{rid}/{package}/...</c> (post-H1 layout).</summary>
    PerRid,

    /// <summary>Licenses written under <c>licenses/{package}/...</c> (legacy, library-flat; last-write-wins across RIDs).</summary>
    LibraryFlat,
}
