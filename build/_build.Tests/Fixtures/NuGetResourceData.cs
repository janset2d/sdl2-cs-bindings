using NSubstitute;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace Build.Tests.Fixtures;

internal static class NuGetResourceData
{
    public const string FeedUrl = "https://feed.example.test/v3/index.json";

    public static SourceRepository CreateRepositoryWithoutResources() => new(new PackageSource(FeedUrl), Array.Empty<INuGetResourceProvider>());

    public static (SourceRepository Repository, INuGetResourceProvider Provider, INuGetResource Resource) CreateRepositoryWithResource()
    {
        var resource = Substitute.For<INuGetResource>();
        var provider = Substitute.For<INuGetResourceProvider>();
        provider.Name.Returns(nameof(NuGetResourceData));
        provider.ResourceType.Returns(typeof(INuGetResource));
        provider.TryCreate(Arg.Any<SourceRepository>(), Arg.Any<CancellationToken>()).Returns(Tuple.Create<bool, INuGetResource?>(true, resource));
        var repository = new SourceRepository(new PackageSource(FeedUrl), [provider]);
        return (repository, provider, resource);
    }
}
