using Build.Targets.PublishStaging.Services;
using Build.Tests.Fixtures;
using NSubstitute;
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
    public async Task GetRequiredAsync_Should_Return_The_Resource_And_Forward_The_Token()
    {
        var (repository, provider, resource) = NuGetResourceData.CreateRepositoryWithResource();
        using var cancellation = new CancellationTokenSource();

        var result = await NuGetResources.GetRequiredAsync<INuGetResource>(repository, cancellation.Token);

        await Assert.That(result).IsSameReferenceAs(resource);
        await provider.Received(1).TryCreate(repository, cancellation.Token);
    }
}
