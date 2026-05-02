using Build.Features.Packaging;
using Build.Host.Configuration;
using Build.Host.Paths;
using Build.Integrations.DotNet;
using Build.Shared.Runtime;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Unit.Features.Packaging;

/// <summary>
/// Targeted tests for <see cref="PackageConsumerSmokePipeline"/> contract boundaries.
/// Full orchestration testing (feed probe, compile-sanity, per-TFM smoke) is deferred
/// until the consumer smoke csproj infra stabilizes; these tests cover the guard rails.
/// </summary>
public sealed class PackageConsumerSmokeRunnerTests
{
    [Test]
    public async Task RunAsync_Should_Throw_CakeException_When_Versions_Empty()
    {
        // C.8 strict: empty mapping → CakeException before any work begins.
        var runner = CreateMinimalRunner();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var request = new PackageConsumerSmokeRequest(
            "win-x64",
            new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase),
            new DirectoryPath("artifacts/packages"));

        var exception = await Assert.ThrowsAsync<Cake.Core.CakeException>(
            () => runner.RunAsync(repo.BuildContext, request));
        await Assert.That(exception!.Message).Contains("non-empty version mapping");
    }

    [Test]
    public async Task RunAsync_Should_Throw_ArgumentNullException_When_Request_Null()
    {
        var runner = CreateMinimalRunner();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => runner.RunAsync(repo.BuildContext, null!));
    }

    private static PackageConsumerSmokePipeline CreateMinimalRunner()
    {
        var cakeContext = Substitute.For<ICakeContext>();
        cakeContext.Log.Returns(Substitute.For<ICakeLog>());
        var log = Substitute.For<ICakeLog>();
        var pathService = Substitute.For<IPathService>();
        var manifestConfig = Fixtures.ManifestFixture.CreateTestManifestConfig();
        var dotNetConfig = new DotNetBuildConfiguration("Release");
        var projectMetadataReader = Substitute.For<IProjectMetadataReader>();
        var dotNetRuntimeEnvironment = Substitute.For<IDotNetRuntimeEnvironment>();

        return new PackageConsumerSmokePipeline(
            cakeContext, log, pathService, manifestConfig, dotNetConfig, projectMetadataReader, dotNetRuntimeEnvironment);
    }
}
