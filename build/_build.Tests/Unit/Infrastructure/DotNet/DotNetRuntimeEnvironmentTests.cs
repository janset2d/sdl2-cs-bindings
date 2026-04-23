using Build.Domain.Runtime;
using Build.Infrastructure.DotNet;
using Cake.Core.Diagnostics;
using NSubstitute;

namespace Build.Tests.Unit.Infrastructure.DotNet;

public sealed class DotNetRuntimeEnvironmentTests
{
    [Test]
    public async Task ResolveRuntimeChannels_Should_Return_Distinct_Modern_Runtime_Channels()
    {
        var channels = DotNetRuntimeEnvironment.ResolveRuntimeChannels(
            ["net9.0", "net8.0", "net462", "netstandard2.0", "net9.0-windows"]);

        await Assert.That(channels).IsEquivalentTo(["8.0", "9.0"]);
    }

    [Test]
    public async Task ResolveRuntimeChannels_Should_Return_Empty_When_No_Modern_Runtime_Tfms()
    {
        var channels = DotNetRuntimeEnvironment.ResolveRuntimeChannels(
            ["net462", "netstandard2.0"]);

        await Assert.That(channels).IsEmpty();
    }

    [Test]
    public async Task ResolveAsync_Should_Return_Empty_When_Rid_Is_Not_WinX86()
    {
        var resolver = new DotNetRuntimeEnvironment(Substitute.For<ICakeLog>());

        var environment = await resolver.ResolveAsync("linux-x64", ["net9.0"]);

        await Assert.That(environment).IsEmpty();
    }

    [Test]
    public async Task ResolveAsync_Should_Throw_PlatformNotSupportedException_When_WinX86_On_NonWindows_Host()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var resolver = new DotNetRuntimeEnvironment(Substitute.For<ICakeLog>());

        var thrown = await Assert.That(async () => await resolver.ResolveAsync("win-x86", ["net9.0"])).Throws<PlatformNotSupportedException>();
        await Assert.That(thrown!.Message).Contains("win-x86");
        await Assert.That(thrown.Message).Contains("non-Windows");
    }

    [Test]
    public async Task Resolver_Should_Implement_IDotNetRuntimeEnvironment_Contract()
    {
        var resolver = new DotNetRuntimeEnvironment(Substitute.For<ICakeLog>());

        await Assert.That(resolver).IsAssignableTo<IDotNetRuntimeEnvironment>();
    }
}
