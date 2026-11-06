using Build.Data.Versions;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Unit.Data.Versions;
/// <summary>
/// Mock-based unit coverage for <see cref="VersionFileRepository"/> — constructor + argument
/// validation. End-to-end Load/SaveAsync round-trip against FakeFileSystem lives in
/// <see cref="VersionFileRepositoryRoundTripTests"/>.
/// </summary>
public sealed class VersionFileRepositoryUnitTests
{
    private static readonly PackageFamilyId Sdl2Core = new("sdl2-core");

    [Test]
    public async Task Constructor_Should_Throw_When_CakeContext_Is_Null()
    {
        await Assert.That(() => new VersionFileRepository(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Load_Should_Throw_When_Path_Is_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        var repo = new VersionFileRepository(ctx);
        await Assert.That(() => repo.Load(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SaveAsync_Should_Throw_When_Path_Is_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        var repo = new VersionFileRepository(ctx);
        var versions = new PackageFamilyVersionSet([new PackageFamilyVersion(Sdl2Core, NuGetVersion.Parse("2.32.0"))]);
        await Assert.That(() => repo.SaveAsync(null!, versions)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SaveAsync_Should_Throw_When_Versions_Are_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        var repo = new VersionFileRepository(ctx);
        await Assert.That(() => repo.SaveAsync(new FilePath("artifacts/versions.json"), null!)).Throws<ArgumentNullException>();
    }
}
