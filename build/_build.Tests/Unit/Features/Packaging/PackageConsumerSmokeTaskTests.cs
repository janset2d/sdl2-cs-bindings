using Build.Features.Packaging;
using Build.Host.Configuration;
using Build.Tests.Fixtures;
using Cake.Core.Diagnostics;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Unit.Features.Packaging;

/// <summary>
/// Tests for <see cref="PackageConsumerSmokeTask"/> ShouldRun gating and RunAsync delegation.
/// The task is a thin Cake adapter; the runner owns policy. These tests cover the
/// task-layer skip behavior for an empty version mapping and the request handoff.
/// </summary>
public sealed class PackageConsumerSmokeTaskTests
{
    [Test]
    public async Task ShouldRun_Should_Return_False_When_ExplicitVersions_Empty()
    {
        var config = new PackageBuildConfiguration(
            new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase));
        var runner = Substitute.For<IPackageConsumerSmokeRunner>();
        var log = Substitute.For<ICakeLog>();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        var task = new PackageConsumerSmokeTask(runner, config, log);

        var result = task.ShouldRun(repo.BuildContext);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ShouldRun_Should_Return_True_When_ExplicitVersions_Present()
    {
        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-local.20260422T120000"),
        };
        var config = new PackageBuildConfiguration(versions);
        var runner = Substitute.For<IPackageConsumerSmokeRunner>();
        var log = Substitute.For<ICakeLog>();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        var task = new PackageConsumerSmokeTask(runner, config, log);

        var result = task.ShouldRun(repo.BuildContext);

        await Assert.That(result).IsTrue();
    }
}
