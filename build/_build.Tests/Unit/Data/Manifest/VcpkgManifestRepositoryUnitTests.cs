using Build.Data.Manifest;
using Cake.Core;
using NSubstitute;

namespace Build.Tests.Unit.Data.Manifest;
/// <summary>
/// Mock-based unit coverage for <see cref="VcpkgManifestRepository"/> — argument
/// validation on both the constructor and the per-call <c>Load</c>. End-to-end load
/// behavior (file-existence gate, JSON deserialization, error translation) is covered
/// by <see cref="VcpkgManifestRepositoryRoundTripTests"/>.
/// </summary>
public sealed class VcpkgManifestRepositoryUnitTests
{
    [Test]
    public async Task Constructor_Should_Throw_ArgumentNullException_When_Context_Is_Null()
    {
        await Assert.That(() => new VcpkgManifestRepository(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Load_Should_Throw_ArgumentNullException_When_Path_Is_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        var repository = new VcpkgManifestRepository(ctx);

        await Assert.That(() => repository.Load(null!))
            .Throws<ArgumentNullException>();
    }
}
