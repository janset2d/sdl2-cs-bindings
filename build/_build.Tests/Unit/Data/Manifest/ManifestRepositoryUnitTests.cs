using Build.Data.Manifest;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Data.Manifest;

/// <summary>Constructor guards; <see cref="ManifestRepositoryRoundTripTests"/> covers loading through the fake filesystem.</summary>
public sealed class ManifestRepositoryUnitTests
{
    [Test]
    public async Task Constructor_Should_Throw_When_CakeContext_Is_Null()
    {
        var path = new FilePath("/repo/build/manifest.json");
        await Assert.That(() => new ManifestRepository(null!, path)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_Should_Throw_When_ManifestPath_Is_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        await Assert.That(() => new ManifestRepository(ctx, null!)).Throws<ArgumentNullException>();
    }
}
