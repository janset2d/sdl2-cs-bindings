using Build.Data.Harvest;
using Build.Host.Paths;
using Build.Tests.Fixtures;
using Cake.Core;
using NSubstitute;

namespace Build.Tests.Unit.Data.Harvest;

/// <summary>
/// Mock-based unit coverage for <see cref="HarvestManifestRepository"/> argument validation.
/// File IO behavior lives in <see cref="HarvestManifestRepositoryRoundTripTests"/>.
/// </summary>
public sealed class HarvestManifestRepositoryUnitTests
{
    private const string _libraryName = "sdl2-core";

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

        await Assert.That(() => repository.LoadRidStatusesAsync(_libraryName, cts.Token)).Throws<OperationCanceledException>();
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

        await Assert.That(() => repository.WriteManifestTempAsync(_libraryName, null!)).Throws<ArgumentNullException>();
    }

    private static HarvestManifestRepository CreateRepository()
    {
        var world = FakeCakeWorld.CreateWindows();
        var context = world.CreateBuildContext();
        return new HarvestManifestRepository(world.CakeContext, context.Paths);
    }
}
