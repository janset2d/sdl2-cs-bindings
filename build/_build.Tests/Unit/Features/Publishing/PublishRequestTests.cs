using Build.Features.Publishing;
using NuGet.Versioning;

namespace Build.Tests.Unit.Features.Publishing;

public sealed class PublishRequestTests
{
    [Test]
    public async Task Constructor_Should_Hold_All_Fields()
    {
        const string feedUrl = "https://nuget.pkg.github.com/janset2d/index.json";
        const string authToken = "token-sentinel";
        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-ci.1"),
        };

        var request = new PublishRequest(feedUrl, authToken, versions);

        await Assert.That(request.FeedUrl).IsEqualTo(feedUrl);
        await Assert.That(request.AuthToken).IsEqualTo(authToken);
        await Assert.That(request.Versions["sdl2-core"].ToNormalizedString()).IsEqualTo("2.32.0-ci.1");
    }
}
