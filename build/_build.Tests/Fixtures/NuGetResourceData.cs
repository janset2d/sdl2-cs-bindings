using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace Build.Tests.Fixtures;

internal static class NuGetResourceData
{
    public const string FeedUrl = "https://feed.example.test/v3/index.json";

    public static SourceRepository CreateRepositoryWithoutResources() => new(new PackageSource(FeedUrl), Array.Empty<INuGetResourceProvider>());
}
