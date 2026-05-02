using Build.Features.Publishing;
using Build.Integrations.NuGet;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Unit.Features.Publishing;

public sealed class PublishTaskRunnerTests
{
    private const string FeedUrl = "https://nuget.pkg.github.com/janset2d/index.json";
    private const string AuthToken = "fake-token";

    [Test]
    public async Task RunAsync_Should_Throw_When_FeedUrl_Is_Empty()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();
        var feedClient = Substitute.For<INuGetFeedClient>();

        var runner = new PublishPipeline(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, feedClient);
        var request = new PublishRequest(string.Empty, AuthToken, OneVersion("sdl2-core", "2.32.0-ci.1"));

        var thrown = await Assert.That(() => runner.RunAsync(repo.BuildContext, request)).Throws<CakeException>();
        await Assert.That(thrown!.Message).Contains("non-empty FeedUrl");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_AuthToken_Is_Empty()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();
        var feedClient = Substitute.For<INuGetFeedClient>();

        var runner = new PublishPipeline(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, feedClient);
        var request = new PublishRequest(FeedUrl, string.Empty, OneVersion("sdl2-core", "2.32.0-ci.1"));

        var thrown = await Assert.That(() => runner.RunAsync(repo.BuildContext, request)).Throws<CakeException>();
        await Assert.That(thrown!.Message).Contains("non-empty AuthToken");
        await Assert.That(thrown.Message).Contains("GH_TOKEN");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Versions_Mapping_Is_Empty()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();
        var feedClient = Substitute.For<INuGetFeedClient>();

        var runner = new PublishPipeline(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, feedClient);
        var request = new PublishRequest(FeedUrl, AuthToken, new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase));

        var thrown = await Assert.That(() => runner.RunAsync(repo.BuildContext, request)).Throws<CakeException>();
        await Assert.That(thrown!.Message).Contains("at least one --explicit-version");
    }

    [Test]
    public async Task RunAsync_Should_Refuse_To_Push_local_Suffix_Versions()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();
        SeedPackage(repo, "Janset.SDL2.Core", "2.32.0-local.20260421T000000");
        SeedPackage(repo, "Janset.SDL2.Core.Native", "2.32.0-local.20260421T000000");
        var feedClient = Substitute.For<INuGetFeedClient>();

        var runner = new PublishPipeline(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, feedClient);
        var request = new PublishRequest(FeedUrl, AuthToken, OneVersion("sdl2-core", "2.32.0-local.20260421T000000"));

        var thrown = await Assert.That(() => runner.RunAsync(repo.BuildContext, request)).Throws<CakeException>();
        await Assert.That(thrown!.Message).Contains("local.");
        await Assert.That(thrown.Message).Contains("staging feed");

        await feedClient.DidNotReceive().PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FilePath>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Family_Unknown_To_Manifest()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();
        var feedClient = Substitute.For<INuGetFeedClient>();

        var runner = new PublishPipeline(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, feedClient);
        var request = new PublishRequest(FeedUrl, AuthToken, OneVersion("ghost-family", "1.0.0-ci.1"));

        var thrown = await Assert.That(() => runner.RunAsync(repo.BuildContext, request)).Throws<CakeException>();
        await Assert.That(thrown!.Message).Contains("unknown family 'ghost-family'");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Managed_Nupkg_Missing()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();
        var feedClient = Substitute.For<INuGetFeedClient>();

        var runner = new PublishPipeline(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, feedClient);
        var request = new PublishRequest(FeedUrl, AuthToken, OneVersion("sdl2-core", "2.32.0-ci.1"));

        var thrown = await Assert.That(() => runner.RunAsync(repo.BuildContext, request)).Throws<CakeException>();
        await Assert.That(thrown!.Message).Contains("Janset.SDL2.Core.2.32.0-ci.1.nupkg");
        await Assert.That(thrown.Message).Contains("not found");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Native_Nupkg_Missing_But_Managed_Present()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();
        SeedPackage(repo, "Janset.SDL2.Core", "2.32.0-ci.1");
        var feedClient = Substitute.For<INuGetFeedClient>();

        var runner = new PublishPipeline(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, feedClient);
        var request = new PublishRequest(FeedUrl, AuthToken, OneVersion("sdl2-core", "2.32.0-ci.1"));

        var thrown = await Assert.That(() => runner.RunAsync(repo.BuildContext, request)).Throws<CakeException>();
        await Assert.That(thrown!.Message).Contains("Janset.SDL2.Core.Native.2.32.0-ci.1.nupkg");
    }

    [Test]
    public async Task RunAsync_Should_Push_Managed_And_Native_Per_Family_On_Happy_Path()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();
        SeedPackage(repo, "Janset.SDL2.Core", "2.32.0-ci.1");
        SeedPackage(repo, "Janset.SDL2.Core.Native", "2.32.0-ci.1");
        SeedPackage(repo, "Janset.SDL2.Image", "2.8.0-ci.1");
        SeedPackage(repo, "Janset.SDL2.Image.Native", "2.8.0-ci.1");

        var feedClient = Substitute.For<INuGetFeedClient>();
        var runner = new PublishPipeline(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, feedClient);

        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-ci.1"),
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-ci.1"),
        };
        var request = new PublishRequest(FeedUrl, AuthToken, versions);

        await runner.RunAsync(repo.BuildContext, request);

        // 2 families × (managed + native) = 4 pushes.
        await feedClient.Received(4).PushAsync(FeedUrl, AuthToken, Arg.Any<FilePath>(), Arg.Any<CancellationToken>());

        await feedClient.Received(1).PushAsync(FeedUrl, AuthToken,
            Arg.Is<FilePath>(f => f.GetFilename().FullPath == "Janset.SDL2.Core.2.32.0-ci.1.nupkg"),
            Arg.Any<CancellationToken>());
        await feedClient.Received(1).PushAsync(FeedUrl, AuthToken,
            Arg.Is<FilePath>(f => f.GetFilename().FullPath == "Janset.SDL2.Core.Native.2.32.0-ci.1.nupkg"),
            Arg.Any<CancellationToken>());
        await feedClient.Received(1).PushAsync(FeedUrl, AuthToken,
            Arg.Is<FilePath>(f => f.GetFilename().FullPath == "Janset.SDL2.Image.2.8.0-ci.1.nupkg"),
            Arg.Any<CancellationToken>());
        await feedClient.Received(1).PushAsync(FeedUrl, AuthToken,
            Arg.Is<FilePath>(f => f.GetFilename().FullPath == "Janset.SDL2.Image.Native.2.8.0-ci.1.nupkg"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_Should_Reject_Stable_Versions_With_local_Label_Case_Insensitively()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();
        SeedPackage(repo, "Janset.SDL2.Core", "2.32.0-LOCAL.X");
        SeedPackage(repo, "Janset.SDL2.Core.Native", "2.32.0-LOCAL.X");
        var feedClient = Substitute.For<INuGetFeedClient>();

        var runner = new PublishPipeline(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, feedClient);
        var request = new PublishRequest(FeedUrl, AuthToken, OneVersion("sdl2-core", "2.32.0-LOCAL.X"));

        var thrown = await Assert.That(() => runner.RunAsync(repo.BuildContext, request)).Throws<CakeException>();
        await Assert.That(thrown!.Message).Contains("local.");
    }

    private static Dictionary<string, NuGetVersion> OneVersion(string family, string version) =>
        new(StringComparer.OrdinalIgnoreCase) { [family] = NuGetVersion.Parse(version) };

    private static void SeedPackage(FakeRepoHandles repo, string packageId, string version)
    {
        var packagePath = repo.Paths.GetPackageOutputFile(packageId, version);
        var directory = repo.FileSystem.GetDirectory(packagePath.GetDirectory());
        if (!directory.Exists)
        {
            directory.Create();
        }

        var file = repo.FileSystem.GetFile(packagePath);
        using var stream = file.OpenWrite();
        using var writer = new StreamWriter(stream);
        writer.Write("nupkg-stub");
    }
}
