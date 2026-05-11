using System.Collections.Immutable;
using System.IO.Compression;
using Build.Data.NativePackageMetadata;
using Build.Host.Cake;
using Build.Tests.Fixtures;
using Cake.Common.IO;
using Cake.Core;
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
        var world = FakeCakeWorldV2.CreateWindows();
        return new NativePackageMetadataRepository(world.CakeContext);
    }
}

public sealed class NativePackageMetadataRepositoryRoundTripTests
{
    private const string MetadataEntryName = "janset-native-metadata.json";

    [Test]
    public async Task WriteAsync_Should_Write_Metadata_Json()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var repository = new NativePackageMetadataRepository(world.CakeContext);
        var metadata = NativeMetadataFixture.Create();
        var path = world.RepoRoot.CombineWithFilePath("artifacts/harvest_output/sdl2/janset-native-metadata.json");

        await repository.WriteAsync(path, metadata);

        await Assert.That(world.CakeContext.FileExists(path)).IsTrue();
        await Assert.That(await world.CakeContext.ReadAllTextAsync(path))
            .IsEqualTo(world.CakeContext.SerializeJson(metadata));
    }

    [Test]
    public async Task ReadFromPackageAsync_Should_Return_Metadata_When_Package_Contains_Valid_Metadata()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var repository = new NativePackageMetadataRepository(world.CakeContext);
        var packagePath = PackagePath(world);
        SeedNupkgWithEntries(world, packagePath,
        [
            (MetadataEntryName, FixtureLoader.Load("NativePackageMetadata/native-metadata-valid.json")),
            ("package.nuspec", "<package />"),
        ]);

        var result = await repository.ReadFromPackageAsync(packagePath);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.FamilyIdentifier).IsEqualTo("sdl2-core");
        await Assert.That(result.Value.TripletSet).Contains("x64-windows-hybrid");
    }

    [Test]
    public async Task ReadFromPackageAsync_Should_Return_Failure_When_Package_Is_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var repository = new NativePackageMetadataRepository(world.CakeContext);
        var packagePath = PackagePath(world);

        var result = await repository.ReadFromPackageAsync(packagePath);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Message).Contains("missing", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task ReadFromPackageAsync_Should_Return_Failure_When_Package_Is_Not_A_Zip()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("artifacts/packages/Janset.SDL2.Core.Native.2.32.0.nupkg", "not a zip");
        var repository = new NativePackageMetadataRepository(world.CakeContext);

        var result = await repository.ReadFromPackageAsync(PackagePath(world));

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Message).Contains("could not be read", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task ReadFromPackageAsync_Should_Return_Failure_When_Metadata_Entry_Is_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var repository = new NativePackageMetadataRepository(world.CakeContext);
        var packagePath = PackagePath(world);
        SeedNupkgWithEntries(world, packagePath, [("package.nuspec", "<package />")]);

        var result = await repository.ReadFromPackageAsync(packagePath);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Message).Contains("does not contain root metadata file", StringComparison.Ordinal);
    }

    [Test]
    public async Task ReadFromPackageAsync_Should_Return_Failure_When_Metadata_Json_Is_Invalid()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var repository = new NativePackageMetadataRepository(world.CakeContext);
        var packagePath = PackagePath(world);
        SeedNupkgWithEntries(world, packagePath, [(MetadataEntryName, FixtureLoader.Load("NativePackageMetadata/native-metadata-invalid.json"))]);

        var result = await repository.ReadFromPackageAsync(packagePath);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Message).Contains("not valid JSON", StringComparison.Ordinal);
    }

    [Test]
    public async Task ReadFromPackageAsync_Should_Return_Failure_When_Metadata_Deserializes_To_Null()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var repository = new NativePackageMetadataRepository(world.CakeContext);
        var packagePath = PackagePath(world);
        SeedNupkgWithEntries(world, packagePath, [(MetadataEntryName, FixtureLoader.Load("NativePackageMetadata/native-metadata-null.json"))]);

        var result = await repository.ReadFromPackageAsync(packagePath);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Message).Contains("deserialized to null", StringComparison.Ordinal);
    }

    private static FilePath PackagePath(FakeCakeWorldV2 world)
        => world.RepoRoot.CombineWithFilePath("artifacts/packages/Janset.SDL2.Core.Native.2.32.0.nupkg");

    private static void SeedNupkgWithEntries(FakeCakeWorldV2 world, FilePath nupkgPath, ImmutableArray<(string Entry, string Content)> entries)
    {
        var directory = world.FileSystem.GetDirectory(nupkgPath.GetDirectory());
        if (!directory.Exists)
        {
            directory.Create();
        }

        var file = world.FileSystem.GetFile(nupkgPath);
        using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
    }
}

internal static class NativeMetadataFixture
{
    public static NativePackageMetadataDocument Create() => new()
    {
        JansetFamilyVersion = "2.32.0",
        FamilyIdentifier = "sdl2-core",
        UpstreamLibrary = "sdl2",
        UpstreamVersion = "2.32.8",
        VcpkgPortVersion = 0,
        TripletSet = ["x64-windows-hybrid", "x64-linux-hybrid"],
        BuildCommit = "0123456789abcdef0123456789abcdef01234567",
    };
}
