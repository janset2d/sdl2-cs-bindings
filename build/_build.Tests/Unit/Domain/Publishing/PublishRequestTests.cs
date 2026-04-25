using Build.Domain.Publishing.Models;
using Cake.Core.IO;

namespace Build.Tests.Unit.Domain.Publishing;

/// <summary>
/// Shape sanity for the <see cref="PublishRequest"/> record.
/// The request stays stable even while publish execution is still stubbed.
/// </summary>
public sealed class PublishRequestTests
{
    [Test]
    public async Task Constructor_Should_Hold_Packages_Dir_Feed_Url_And_Token()
    {
        var packagesDir = new DirectoryPath("artifacts/packages");
        const string feedUrl = "https://nuget.pkg.github.com/janset2d/index.json";
        const string authToken = "token-sentinel";

        var request = new PublishRequest(packagesDir, feedUrl, authToken);

        await Assert.That(request.PackagesDir.FullPath).IsEqualTo("artifacts/packages");
        await Assert.That(request.FeedUrl).IsEqualTo(feedUrl);
        await Assert.That(request.AuthToken).IsEqualTo(authToken);
    }
}
