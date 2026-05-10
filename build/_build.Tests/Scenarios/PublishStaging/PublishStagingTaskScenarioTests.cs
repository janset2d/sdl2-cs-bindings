using Build.Targets.PublishStaging.Services;
using Build.Repositories;
using Build.Targets.PublishStaging;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Build.Tests.Scenarios.PublishStaging;

/// <summary>
/// In-process behavior coverage for <c>PublishStagingTask</c>: exercises real task
/// orchestration against a fake Cake world with a substituted <see cref="INuGetFeedClient"/>.
/// Happy path pushes a managed + native nupkg pair per family; failure paths cover the
/// boundary preconditions (versions empty, auth token missing, local-suffix guard).
/// </summary>
public sealed class PublishStagingTaskScenarioTests
{
    [Test]
    public async Task RunAsync_Should_Push_Managed_And_Native_Pair_Per_Family_When_Happy_Path()
    {
        var feedClient = Substitute.For<INuGetFeedClient>();
        var world = NewWorld(feedClient);
        // Multi-family fixture: sdl2-core@2.32.0 + sdl2-image@2.8.0.
        SeedFeedNupkgs(world, ("sdl2-core", "2.32.0"), ("sdl2-image", "2.8.0"));
        world.Environment.SetEnvironmentVariable("GH_TOKEN", "test-token");

        var result = await CreateHost(world, feedClient).RunAsync();

        await Assert.That(result.Exception).IsNull();
        await Assert.That(result.Success).IsTrue();
        // 2 families × (managed + native) = 4 pushes
        await feedClient.Received(4).PushAsync(
            Arg.Is<string>(url => url.Contains("nuget.pkg.github.com", StringComparison.Ordinal)),
            Arg.Is<string>(token => token == "test-token"),
            Arg.Any<FilePath>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_FamilyVersions_Empty()
    {
        var feedClient = Substitute.For<INuGetFeedClient>();
        var world = NewWorld(feedClient);
        world.WithVersionsFile(null);

        var result = await CreateHost(world, feedClient).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("requires --versions-file", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Auth_Token_Missing()
    {
        var feedClient = Substitute.For<INuGetFeedClient>();
        var world = NewWorld(feedClient);
        SeedFeedNupkgs(world, ("sdl2-core", "2.32.0"), ("sdl2-image", "2.8.0"));
        // No GH_TOKEN / GITHUB_TOKEN set.

        var result = await CreateHost(world, feedClient).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("auth token", StringComparison.Ordinal);
        await Assert.That(result.Exception.Message).Contains("GH_TOKEN", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Refuse_Push_When_Version_Has_Local_Suffix()
    {
        var feedClient = Substitute.For<INuGetFeedClient>();
        var world = NewWorld(feedClient);
        SeedVersionsFromFixture(world, "Versions/versions-local-suffix.json");
        SeedFeedNupkgs(world, ("sdl2-core", "2.32.0-local.20260421T120000"));
        world.Environment.SetEnvironmentVariable("GH_TOKEN", "test-token");

        var result = await CreateHost(world, feedClient).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("local.", StringComparison.Ordinal);
        await feedClient.DidNotReceiveWithAnyArgs().PushAsync(default!, default!, default!, default);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Required_Nupkg_Missing_From_Output_Dir()
    {
        var feedClient = Substitute.For<INuGetFeedClient>();
        var world = NewWorld(feedClient);
        // Multi-family fixture (sdl2-core@2.32.0 + sdl2-image@2.8.0); seed only sdl2-core nupkgs.
        world.WithBinaryFile("artifacts/packages/Janset.SDL2.Core.2.32.0.nupkg", []);
        world.WithBinaryFile("artifacts/packages/Janset.SDL2.Core.Native.2.32.0.nupkg", []);
        world.Environment.SetEnvironmentVariable("GH_TOKEN", "test-token");

        var result = await CreateHost(world, feedClient).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("not found", StringComparison.Ordinal);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private const string VersionsFilePath = "artifacts/resolve-versions/versions.json";

    private static FakeCakeWorldV2 NewWorld(INuGetFeedClient feedClient)
    {
        ArgumentNullException.ThrowIfNull(feedClient);
        return FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithVersionsFile(VersionsFilePath)
            .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-multi-family.json"));
    }

    private static void SeedVersionsFromFixture(FakeCakeWorldV2 world, string fixtureRelativePath)
    {
        world.WithTextFile(VersionsFilePath, FixtureLoader.Load(fixtureRelativePath));
    }

    private static void SeedFeedNupkgs(FakeCakeWorldV2 world, params (string Family, string Version)[] entries)
    {
        foreach (var (family, version) in entries)
        {
            var infix = family switch
            {
                "sdl2-core" => "SDL2.Core",
                "sdl2-image" => "SDL2.Image",
                _ => family,
            };
            world.WithBinaryFile($"artifacts/packages/Janset.{infix}.{version}.nupkg", []);
            world.WithBinaryFile($"artifacts/packages/Janset.{infix}.Native.{version}.nupkg", []);
        }
    }

    private static TargetTestHostV2<PublishStagingTask> CreateHost(FakeCakeWorldV2 world, INuGetFeedClient feedClient)
    {
        feedClient.PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return new TargetTestHostV2<PublishStagingTask>(world)
            .WithManifest(ManifestFixture.CreateTestManifestConfig())
            .WithServices(services =>
            {
                services.AddRepositories();
                services.AddPublishStaging();
                services.AddSingleton(feedClient);
            });
    }
}
