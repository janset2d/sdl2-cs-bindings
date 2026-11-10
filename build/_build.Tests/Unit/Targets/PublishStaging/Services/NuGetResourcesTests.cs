using Build.Targets.PublishStaging.Services;
using Build.Tests.Fixtures;
using NuGet.Protocol.Core.Types;

namespace Build.Tests.Unit.Targets.PublishStaging.Services;

public sealed class NuGetResourcesTests
{
    [Test]
    public async Task GetRequiredAsync_Should_Reject_A_Feed_Without_Package_Discovery()
    {
        var repository = NuGetResourceData.CreateRepositoryWithoutResources();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => NuGetResources.GetRequiredAsync<FindPackageByIdResource>(repository));

        await Assert.That(exception?.Message).IsEqualTo($"The source '{NuGetResourceData.FeedUrl}' does not provide FindPackageByIdResource.");
    }

    [Test]
    public async Task GetRequiredAsync_Should_Reject_A_Feed_Without_Package_Updates()
    {
        var repository = NuGetResourceData.CreateRepositoryWithoutResources();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => NuGetResources.GetRequiredAsync<PackageUpdateResource>(repository));

        await Assert.That(exception?.Message).IsEqualTo($"The source '{NuGetResourceData.FeedUrl}' does not provide PackageUpdateResource.");
    }
}
