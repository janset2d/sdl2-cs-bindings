using Build.Domain.Publishing.Models;
using Cake.Core.IO;

namespace Build.Tests.Unit.Domain.Publishing;

/// <summary>
/// Shape sanity for the ADR-003 §3.2 <see cref="PublishRequest"/> record. The Publish stage
/// itself is a Slice E stub; the record shape is locked in Slice C so Phase 2b Stream D-ci
/// can consume the contract without retroactive record churn.
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
