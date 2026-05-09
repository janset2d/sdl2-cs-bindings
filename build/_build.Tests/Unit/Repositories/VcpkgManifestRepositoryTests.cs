using System.Collections.Immutable;
using Build.Integrations.Vcpkg;
using Build.Repositories;
using Build.Shared.Manifest;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Repositories;

/// <summary>
/// Mock-based unit coverage for <see cref="VcpkgManifestRepository"/> — constructor argument
/// validation. End-to-end load behavior (file-existence gate, reader delegation) is covered
/// by <see cref="VcpkgManifestRepositoryRoundTripTests"/>.
/// </summary>
public sealed class VcpkgManifestRepositoryUnitTests
{
    [Test]
    public async Task Constructor_Should_Throw_ArgumentNullException_When_Context_Is_Null()
    {
        var reader = Substitute.For<IVcpkgManifestReader>();
        var path = new FilePath("/repo/vcpkg.json");

        await Assert.That(() => new VcpkgManifestRepository(null!, reader, path))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_Should_Throw_ArgumentNullException_When_Reader_Is_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        var path = new FilePath("/repo/vcpkg.json");

        await Assert.That(() => new VcpkgManifestRepository(ctx, null!, path))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_Should_Throw_ArgumentNullException_When_Path_Is_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        var reader = Substitute.For<IVcpkgManifestReader>();

        await Assert.That(() => new VcpkgManifestRepository(ctx, reader, null!))
            .Throws<ArgumentNullException>();
    }
}

/// <summary>
/// Sociable round-trip coverage for <see cref="VcpkgManifestRepository"/>. Pairs with
/// <see cref="VcpkgManifestRepositoryUnitTests"/> per the repository-cohort rule.
/// </summary>
public sealed class VcpkgManifestRepositoryRoundTripTests
{
    [Test]
    public async Task Load_Should_Return_Parsed_Manifest_When_File_Exists()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("vcpkg.json", """{"overrides":[{"name":"sdl2","version":"2.32.0"}]}""");

        var fakeManifest = new VcpkgManifest
        {
            Overrides = ImmutableArray.Create(new VcpkgOverride { Name = "sdl2", Version = "2.32.0" }),
        };
        var reader = Substitute.For<IVcpkgManifestReader>();
        reader.ParseFile(Arg.Any<FilePath>()).Returns(fakeManifest);

        var path = world.RepoRoot.CombineWithFilePath("vcpkg.json");
        var repository = new VcpkgManifestRepository(world.CakeContext, reader, path);

        var loaded = repository.Load();

        await Assert.That(loaded).IsSameReferenceAs(fakeManifest);
        reader.Received(1).ParseFile(path);
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_File_Is_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reader = Substitute.For<IVcpkgManifestReader>();
        var path = world.RepoRoot.CombineWithFilePath("vcpkg.json");
        var repository = new VcpkgManifestRepository(world.CakeContext, reader, path);

        var ex = await Assert.That(() => repository.Load()).Throws<CakeException>();

        await Assert.That(ex!.Message).Contains("vcpkg manifest");
        await Assert.That(ex!.Message).Contains("does not exist");
        reader.DidNotReceive().ParseFile(Arg.Any<FilePath>());
    }
}
