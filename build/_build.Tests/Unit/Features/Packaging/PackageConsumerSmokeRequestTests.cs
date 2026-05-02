using Build.Features.Packaging;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Tests.Unit.Features.Packaging;

/// <summary>
/// Shape sanity for the <see cref="PackageConsumerSmokeRequest"/> record.
/// Each RID runner constructs one request per matrix entry.
/// </summary>
public sealed class PackageConsumerSmokeRequestTests
{
    [Test]
    public async Task Constructor_Should_Hold_Rid_Versions_And_Feed_Path()
    {
        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-local.20260421T120000"),
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-local.20260421T120000"),
        };
        var feedPath = new DirectoryPath("artifacts/packages");

        var request = new PackageConsumerSmokeRequest("win-x64", versions, feedPath);

        await Assert.That(request.Rid).IsEqualTo("win-x64");
        await Assert.That(request.Versions).IsSameReferenceAs(versions);
        await Assert.That(request.FeedPath.FullPath).IsEqualTo("artifacts/packages");
    }
}
