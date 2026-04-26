using System.IO;
using System.Xml.Linq;
using Build.Application.Packaging;
using Build.Domain.Packaging.Models;
using Build.Infrastructure.DotNet;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Unit.Application.Packaging;

/// <summary>
/// Tests for <see cref="RemoteArtifactSourceResolver"/>. The feed client is mocked via
/// NSubstitute so the resolver's behaviour can be verified deterministically without
/// hitting a real GitHub Packages feed. Auth-token discovery uses the
/// Cake.Testing <c>FakeEnvironment.SetEnvironmentVariable</c> seam.
/// </summary>
public sealed class RemoteArtifactSourceResolverTests
{
    private const string ExpectedFeedUrl = "https://nuget.pkg.github.com/janset2d/index.json";

    [Test]
    public async Task PrepareFeedAsync_Should_Throw_When_Auth_Token_Env_Vars_Are_Unset()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();
        var feedClient = Substitute.For<INuGetFeedClient>();

        var resolver = new RemoteArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            feedClient);

        var thrown = await Assert.That(() =>
                resolver.PrepareFeedAsync(repo.BuildContext, EmptyVersions()))
            .Throws<CakeException>();

        await Assert.That(thrown!.Message).Contains("GH_TOKEN");
        await Assert.That(thrown.Message).Contains("GITHUB_TOKEN");
        await Assert.That(thrown.Message).Contains("gh auth token");
    }

    [Test]
    public async Task PrepareFeedAsync_Should_Use_GH_TOKEN_When_Set()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();
        repo.Environment.SetEnvironmentVariable("GH_TOKEN", "gh-token-value");

        var feedClient = CreateFeedClient(latestVersion: NuGetVersion.Parse("2.32.0-preview.1"));
        var resolver = new RemoteArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            feedClient);

        await resolver.PrepareFeedAsync(repo.BuildContext, EmptyVersions());

        await feedClient.Received().GetLatestVersionAsync(
            ExpectedFeedUrl,
            "gh-token-value",
            Arg.Any<string>(),
            includePrerelease: true,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PrepareFeedAsync_Should_Fall_Back_To_GITHUB_TOKEN_When_GH_TOKEN_Unset()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();
        repo.Environment.SetEnvironmentVariable("GITHUB_TOKEN", "github-token-value");

        var feedClient = CreateFeedClient(latestVersion: NuGetVersion.Parse("2.32.0-preview.1"));
        var resolver = new RemoteArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            feedClient);

        await resolver.PrepareFeedAsync(repo.BuildContext, EmptyVersions());

        await feedClient.Received().GetLatestVersionAsync(
            ExpectedFeedUrl,
            "github-token-value",
            Arg.Any<string>(),
            includePrerelease: true,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PrepareFeedAsync_Should_Wipe_Existing_Local_Feed_Before_Pulling()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithTextFile("artifacts/packages/stale-from-prior-local-pack.txt", "stale")
            .BuildContextWithHandles();
        repo.Environment.SetEnvironmentVariable("GH_TOKEN", "gh-token-value");

        var feedClient = CreateFeedClient(latestVersion: NuGetVersion.Parse("2.32.0-preview.1"));
        var resolver = new RemoteArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            feedClient);

        await resolver.PrepareFeedAsync(repo.BuildContext, EmptyVersions());

        var stale = repo.FileSystem.GetFile(repo.Paths.PackagesOutput.CombineWithFilePath("stale-from-prior-local-pack.txt"));
        await Assert.That(stale.Exists).IsFalse();
    }

    [Test]
    public async Task PrepareFeedAsync_Should_Throw_When_Managed_Has_No_Published_Version()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();
        repo.Environment.SetEnvironmentVariable("GH_TOKEN", "gh-token-value");

        var feedClient = Substitute.For<INuGetFeedClient>();
        feedClient
            .GetLatestVersionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((NuGetVersion?)null);

        var resolver = new RemoteArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            feedClient);

        var thrown = await Assert.That(() =>
                resolver.PrepareFeedAsync(repo.BuildContext, EmptyVersions()))
            .Throws<CakeException>();

        await Assert.That(thrown!.Message).Contains("could not find any published version");
        await Assert.That(thrown.Message).Contains("--source=local");
    }

    [Test]
    public async Task PrepareFeedAsync_Should_Throw_When_Managed_And_Native_Versions_Disagree()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();
        repo.Environment.SetEnvironmentVariable("GH_TOKEN", "gh-token-value");

        var feedClient = Substitute.For<INuGetFeedClient>();
        feedClient
            .GetLatestVersionAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Is<string>(id => id.EndsWith(".Native", StringComparison.Ordinal)),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(NuGetVersion.Parse("2.32.0-preview.2"));
        feedClient
            .GetLatestVersionAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Is<string>(id => !id.EndsWith(".Native", StringComparison.Ordinal)),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(NuGetVersion.Parse("2.32.0-preview.1"));

        var resolver = new RemoteArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            feedClient);

        var thrown = await Assert.That(() =>
                resolver.PrepareFeedAsync(repo.BuildContext, EmptyVersions()))
            .Throws<CakeException>();

        await Assert.That(thrown!.Message).Contains("family-version invariant violation");
        await Assert.That(thrown.Message).Contains("2.32.0-preview.1");
        await Assert.That(thrown.Message).Contains("2.32.0-preview.2");
    }

    [Test]
    public async Task PrepareFeedAsync_Should_Download_Both_Managed_And_Native_Per_Concrete_Family()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var concreteFamilies = manifest.PackageFamilies
            .Where(f => !string.IsNullOrWhiteSpace(f.ManagedProject) && !string.IsNullOrWhiteSpace(f.NativeProject))
            .ToList();

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();
        repo.Environment.SetEnvironmentVariable("GH_TOKEN", "gh-token-value");

        var version = NuGetVersion.Parse("2.32.0-preview.1");
        var feedClient = CreateFeedClient(latestVersion: version);

        var resolver = new RemoteArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            feedClient);

        await resolver.PrepareFeedAsync(repo.BuildContext, EmptyVersions());

        // Each family contributes 2 downloads (managed + native).
        await feedClient.Received(concreteFamilies.Count * 2).DownloadAsync(
            ExpectedFeedUrl,
            "gh-token-value",
            Arg.Any<string>(),
            version,
            repo.Paths.PackagesOutput,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task WriteConsumerOverrideAsync_Should_Throw_When_PrepareFeed_Was_Not_Called()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();
        var feedClient = Substitute.For<INuGetFeedClient>();

        var resolver = new RemoteArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            feedClient);

        var thrown = await Assert.That(() =>
                resolver.WriteConsumerOverrideAsync(repo.BuildContext, EmptyVersions()))
            .Throws<CakeException>();

        await Assert.That(thrown!.Message).Contains("called before PrepareFeedAsync");
    }

    [Test]
    public async Task PrepareFeedAsync_And_WriteConsumerOverrideAsync_Should_Stamp_Local_Props_With_Resolved_Mapping()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();
        repo.Environment.SetEnvironmentVariable("GH_TOKEN", "gh-token-value");

        var version = NuGetVersion.Parse("2.32.0-preview.1");
        var feedClient = CreateFeedClient(latestVersion: version);

        var resolver = new RemoteArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            feedClient);

        await resolver.PrepareFeedAsync(repo.BuildContext, EmptyVersions());
        await resolver.WriteConsumerOverrideAsync(repo.BuildContext, EmptyVersions());

        var propsPath = repo.Paths.GetLocalPropsFile();
        var file = repo.FileSystem.GetFile(propsPath);
        await Assert.That(file.Exists).IsTrue();

        string xml;
        using (var stream = file.OpenRead())
        using (var reader = new StreamReader(stream))
        {
            xml = await reader.ReadToEndAsync();
        }

        var document = XDocument.Parse(xml);
        var propertyGroup = document.Root?.Element("PropertyGroup");
        await Assert.That(propertyGroup).IsNotNull();

        await Assert.That(propertyGroup!.Element("LocalPackageFeed")?.Value)
            .IsEqualTo(repo.Paths.PackagesOutput.FullPath);
        await Assert.That(propertyGroup.Element("JansetSdl2CorePackageVersion")?.Value)
            .IsEqualTo(version.ToNormalizedString());
        await Assert.That(propertyGroup.Element("JansetSdl2ImagePackageVersion")?.Value)
            .IsEqualTo(version.ToNormalizedString());
    }

    [Test]
    public async Task WriteConsumerOverrideAsync_Should_Prefer_Explicit_Mapping_Over_Cached_Mapping()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();
        repo.Environment.SetEnvironmentVariable("GH_TOKEN", "gh-token-value");

        var cachedVersion = NuGetVersion.Parse("2.32.0-preview.1");
        var explicitVersion = NuGetVersion.Parse("2.32.0-preview.7");

        var feedClient = CreateFeedClient(latestVersion: cachedVersion);
        var resolver = new RemoteArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            feedClient);

        await resolver.PrepareFeedAsync(repo.BuildContext, EmptyVersions());

        var explicitMapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = explicitVersion,
            ["sdl2-image"] = explicitVersion,
        };

        await resolver.WriteConsumerOverrideAsync(repo.BuildContext, explicitMapping);

        var propsPath = repo.Paths.GetLocalPropsFile();
        var file = repo.FileSystem.GetFile(propsPath);
        string xml;
        using (var stream = file.OpenRead())
        using (var reader = new StreamReader(stream))
        {
            xml = await reader.ReadToEndAsync();
        }

        var document = XDocument.Parse(xml);
        var propertyGroup = document.Root!.Element("PropertyGroup")!;

        await Assert.That(propertyGroup.Element("JansetSdl2CorePackageVersion")?.Value)
            .IsEqualTo(explicitVersion.ToNormalizedString());
        await Assert.That(propertyGroup.Element("JansetSdl2ImagePackageVersion")?.Value)
            .IsEqualTo(explicitVersion.ToNormalizedString());
    }

    [Test]
    public async Task Profile_Should_Be_RemoteInternal()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();
        var feedClient = Substitute.For<INuGetFeedClient>();

        var resolver = new RemoteArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            feedClient);

        await Assert.That(resolver.Profile).IsEqualTo(ArtifactProfile.RemoteInternal);
        await Assert.That(resolver.LocalFeedPath.FullPath).IsEqualTo(repo.Paths.PackagesOutput.FullPath);
    }

    private static Dictionary<string, NuGetVersion> EmptyVersions() =>
        new(StringComparer.OrdinalIgnoreCase);

    private static INuGetFeedClient CreateFeedClient(NuGetVersion latestVersion)
    {
        var feedClient = Substitute.For<INuGetFeedClient>();
        feedClient
            .GetLatestVersionAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(latestVersion);

        feedClient
            .DownloadAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<NuGetVersion>(),
                Arg.Any<DirectoryPath>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var packageId = (string)callInfo[2];
                var version = (NuGetVersion)callInfo[3];
                var dir = (DirectoryPath)callInfo[4];
                return Task.FromResult(dir.CombineWithFilePath($"{packageId}.{version.ToNormalizedString()}.nupkg"));
            });

        return feedClient;
    }
}
