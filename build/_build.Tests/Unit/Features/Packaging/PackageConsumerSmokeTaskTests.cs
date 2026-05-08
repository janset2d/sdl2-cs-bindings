using Build.Features.Packaging;
using Build.Host.Configuration;
using Build.Tests.Fixtures;
using Build.Versioning;
using Cake.Core;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Unit.Features.Packaging;

/// <summary>
/// Tests for <see cref="PackageConsumerSmokeTask"/> RunAsync fail-loud gating and delegation.
/// The task is a thin Cake adapter; the runner owns policy. These tests cover the
/// task-layer hard-fail for an empty version mapping and the request handoff.
/// </summary>
public sealed class PackageConsumerSmokeTaskTests
{
    [Test]
    public async Task RunAsync_Should_Throw_When_FamilyVersionMapping_Empty()
    {
        var config = new PackageBuildConfiguration(PackageFamilyVersionSet.Empty);
        var runner = Substitute.For<IPackageConsumerSmokePipeline>();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        var task = new PackageConsumerSmokeTask(runner, config, repo.BuildContext.Runtime, repo.Paths);

        var ex = await Assert.That(async () => await task.RunAsync(repo.BuildContext)).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("--versions-file");
    }

    [Test]
    public async Task RunAsync_Should_Delegate_When_FamilyVersionMapping_Present()
    {
        var versions = new PackageFamilyVersionSet([
            new PackageFamilyVersion(new PackageFamilyId("sdl2-core"), NuGetVersion.Parse("2.32.0-local.20260422T120000")),
        ]);
        var config = new PackageBuildConfiguration(versions);
        var runner = Substitute.For<IPackageConsumerSmokePipeline>();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        var task = new PackageConsumerSmokeTask(runner, config, repo.BuildContext.Runtime, repo.Paths);

        await task.RunAsync(repo.BuildContext);

        await runner.Received(1).RunAsync(Arg.Any<PackageConsumerSmokeRequest>());
    }
}
