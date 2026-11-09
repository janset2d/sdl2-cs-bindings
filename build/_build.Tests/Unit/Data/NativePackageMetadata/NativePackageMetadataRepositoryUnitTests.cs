using Build.Data.NativePackageMetadata;
using Build.Tests.Fixtures;
using Cake.Core.IO;

namespace Build.Tests.Unit.Data.NativePackageMetadata;

public sealed class NativePackageMetadataRepositoryUnitTests
{
    [Test]
    public async Task Constructor_Should_Throw_When_CakeContext_Is_Null()
    {
        await Assert.That(() => new NativePackageMetadataRepository(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task WriteAsync_Should_Throw_When_Path_Is_Null()
    {
        var repository = CreateRepository();
        var metadata = NativeMetadataFixture.Create();

        await Assert.That(() => repository.WriteAsync(null!, metadata)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task WriteAsync_Should_Throw_When_Metadata_Is_Null()
    {
        var repository = CreateRepository();
        var path = new FilePath("artifacts/metadata.json");

        await Assert.That(() => repository.WriteAsync(path, null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task WriteAsync_Should_Throw_When_Cancellation_Is_Already_Requested()
    {
        var repository = CreateRepository();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.That(() => repository.WriteAsync(new FilePath("artifacts/metadata.json"), NativeMetadataFixture.Create(), cts.Token))
            .Throws<OperationCanceledException>();
    }

    private static NativePackageMetadataRepository CreateRepository()
    {
        var world = FakeCakeWorld.CreateWindows();
        return new NativePackageMetadataRepository(world.CakeContext);
    }
}
