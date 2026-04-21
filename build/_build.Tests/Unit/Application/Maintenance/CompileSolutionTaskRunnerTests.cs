using Build.Application.Maintenance;
using Build.Context.Configs;
using Build.Tests.Fixtures;
using Cake.Core.Diagnostics;
using Cake.Testing;
using NSubstitute;

namespace Build.Tests.Unit.Application.Maintenance;

/// <summary>
/// CompileSolutionTaskRunner is a one-line delegation to Cake's <c>DotNetBuild</c> alias; a
/// meaningful unit test would require standing up a full <c>dotnet</c> tool-locator + fake
/// process pipeline to intercept Cake.Common's internal resolution — fragile and low-signal.
/// The end-to-end WSL witness validates the real invocation. These tests focus on ctor guard
/// contracts only.
/// </summary>
public sealed class CompileSolutionTaskRunnerTests
{
    [Test]
    public async Task Ctor_Should_Reject_Null_CakeContext()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var log = new FakeLog();
        var config = new DotNetBuildConfiguration("Release");

        await Assert.That(() => new CompileSolutionTaskRunner(null!, log, repo.Paths, config))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Ctor_Should_Reject_Null_Log()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var config = new DotNetBuildConfiguration("Release");

        await Assert.That(() => new CompileSolutionTaskRunner(repo.CakeContext, null!, repo.Paths, config))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Ctor_Should_Reject_Null_PathService()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var config = new DotNetBuildConfiguration("Release");

        await Assert.That(() => new CompileSolutionTaskRunner(repo.CakeContext, new FakeLog(), null!, config))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Ctor_Should_Reject_Null_BuildConfiguration()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var log = Substitute.For<ICakeLog>();

        await Assert.That(() => new CompileSolutionTaskRunner(repo.CakeContext, log, repo.Paths, null!))
            .Throws<ArgumentNullException>();
    }
}
