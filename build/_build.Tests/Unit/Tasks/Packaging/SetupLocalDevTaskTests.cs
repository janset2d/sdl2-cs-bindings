using Build.Application.Packaging;
using Build.Context;
using Build.Domain.Packaging.Models;
using Build.Tasks.Packaging;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Tasks.Packaging;

public sealed class SetupLocalDevTaskTests
{
    [Test]
    public async Task RunAsync_Should_Prepare_Feed_And_Write_Override_When_Source_Is_Local()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        var resolver = CreateResolver(ArtifactProfile.Local, repo.Paths.PackagesOutput);
        var task = new SetupLocalDevTask(resolver, repo.CakeContext.Log);

        await task.RunAsync(repo.BuildContext);

        await resolver.Received(1)
            .PrepareFeedAsync(repo.BuildContext, Arg.Any<CancellationToken>());
        await resolver.Received(1)
            .WriteConsumerOverrideAsync(repo.BuildContext, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_Should_Skip_Local_Prerequisites_When_Source_Is_Remote()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        var resolver = CreateResolver(ArtifactProfile.RemoteInternal, repo.Paths.PackagesOutput);
        var task = new SetupLocalDevTask(resolver, repo.CakeContext.Log);

        await task.RunAsync(repo.BuildContext);

        await resolver.Received(1)
            .PrepareFeedAsync(repo.BuildContext, Arg.Any<CancellationToken>());
        await resolver.Received(1)
            .WriteConsumerOverrideAsync(repo.BuildContext, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_Should_Skip_Local_Prerequisites_When_Source_Is_Release()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        var resolver = CreateResolver(ArtifactProfile.ReleasePublic, repo.Paths.PackagesOutput);
        var task = new SetupLocalDevTask(resolver, repo.CakeContext.Log);

        await task.RunAsync(repo.BuildContext);

        await resolver.Received(1)
            .PrepareFeedAsync(repo.BuildContext, Arg.Any<CancellationToken>());
        await resolver.Received(1)
            .WriteConsumerOverrideAsync(repo.BuildContext, Arg.Any<CancellationToken>());
    }

    private static IArtifactSourceResolver CreateResolver(ArtifactProfile profile, DirectoryPath localFeedPath)
    {
        var resolver = Substitute.For<IArtifactSourceResolver>();
        resolver.Profile.Returns(profile);
        resolver.LocalFeedPath.Returns(localFeedPath);
        resolver.PrepareFeedAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        resolver.WriteConsumerOverrideAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return resolver;
    }
}
