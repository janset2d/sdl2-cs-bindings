using Build.Application.Packaging;
using Build.Context.Configs;
using Build.Tasks.Packaging;
using Build.Tests.Fixtures;
using Cake.Core.Diagnostics;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Unit.Tasks.Packaging;

/// <summary>
/// Tests for <see cref="PackageConsumerSmokeTask"/> ShouldRun gate + RunAsync delegation.
/// The task is the thin Cake adapter; the runner (<see cref="IPackageConsumerSmokeRunner"/>)
/// owns policy. These tests verify the task-layer auto-skip behavior per ADR-003 §3.2
/// (C.8 strict: empty mapping → skip) and the request-construction → runner delegation.
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
