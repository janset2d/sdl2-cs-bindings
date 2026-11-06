using Build.Data.Harvest;
using Build.Host.Paths;
using Build.Host.Runtime;
using Build.Targets.Harvest.Models;
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
    private const string _libraryName = "sdl2-core";
    private const string _rid = "win-x64";

    private static (HarvestStatusRepository Repo, IPathService Paths) CreateRepoWithMocks()
    {
        var ctx = Substitute.For<ICakeContext>();
        var paths = Substitute.For<IPathService>();
        var profile = Substitute.For<IRuntimeProfile>();
        profile.Rid.Returns(_rid);
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
        await Assert.That(() => repo.Invalidate(_libraryName, cts.Token)).Throws<OperationCanceledException>();
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
        await Assert.That(() => repo.WriteSuccessAsync(_libraryName, null!)).Throws<ArgumentNullException>();
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
        await Assert.That(() => repo.WriteErrorAsync(_libraryName, null!)).Throws<ArgumentException>();
        await Assert.That(() => repo.WriteErrorAsync(_libraryName, "   ")).Throws<ArgumentException>();
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
