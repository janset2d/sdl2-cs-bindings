using Build.Targets.PackageConsumerSmoke.Services;
using Build.Tests.Fixtures;
using Cake.Core.Diagnostics;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.PackageConsumerSmoke.Services;

public sealed class DotNetRuntimeEnvironmentTests
{
    [Test]
    [WindowsOnly]
    public async Task ResolveAsync_Should_Use_Cake_Files_And_Install_Ordered_Channels_Once()
    {
        var world = HostEnvironmentData.RuntimeWorld();
        using var cancellation = new CancellationTokenSource();
        var resolver = new DotNetRuntimeEnvironment(world.CakeContext, world.Log, world.HostProcessRunner);

        var result = await resolver.ResolveAsync("win-x86", ["net10.0", "net8.0", "net10.0"], cancellation.Token);
        var cached = await resolver.ResolveAsync("win-x86", ["net8.0", "net10.0"], cancellation.Token);

        await Assert.That(result.Select(pair => (pair.Key, pair.Value)).ToArray())
            .IsEquivalentTo([("DOTNET_ROOT_X86", HostEnvironmentData.RuntimeRoot), ("DOTNET_ROOT(x86)", HostEnvironmentData.RuntimeRoot)], CollectionOrdering.Matching);
        await Assert.That(cached).IsSameReferenceAs(result);
        await Assert.That(world.HostProcessInvocations.Count).IsEqualTo(2);
        await Assert.That(world.HostProcessInvocations.Select(call => call.Request.Arguments[10])).IsEquivalentTo(["8.0", "10.0"], CollectionOrdering.Matching);
        var invocation = world.HostProcessInvocations[0];
        await Assert.That(invocation.CancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(invocation.Request.FileName).IsEqualTo(HostEnvironmentData.PowerShell);
        await Assert.That(invocation.Request.Arguments).IsEquivalentTo([
            "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", HostEnvironmentData.InstallScript,
            "-Architecture", "x86", "-Channel", "8.0", "-Runtime", "dotnet", "-InstallDir", HostEnvironmentData.RuntimeRoot], CollectionOrdering.Matching);
    }

    [Test]
    [WindowsOnly]
    public async Task ResolveAsync_Should_Report_The_Install_Exit_And_Both_Streams()
    {
        var world = HostEnvironmentData.RuntimeWorld(17, "install output", "install error");
        var resolver = new DotNetRuntimeEnvironment(world.CakeContext, world.Log, world.HostProcessRunner);

        var error = await Assert.That(async () => await resolver.ResolveAsync("win-x86", ["net10.0"])).Throws<InvalidOperationException>();

        await Assert.That(error!.Message).IsEqualTo(
            $"DotNetRuntimeEnvironment: x86 runtime install for channel '10.0' failed with exit code 17.{Environment.NewLine}install output{Environment.NewLine}install error");
    }

    [Test]
    public async Task ResolveRuntimeChannels_Should_Return_Distinct_Modern_Runtime_Channels()
    {
        var channels = DotNetRuntimeEnvironment.ResolveRuntimeChannels(
            ["net10.0", "net9.0", "net8.0", "net462", "netstandard2.0", "net10.0-windows"]);

        await Assert.That(channels).IsEquivalentTo(["8.0", "9.0", "10.0"]);
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
        var world = FakeCakeWorld.CreateLinux();
        var resolver = new DotNetRuntimeEnvironment(world.CakeContext, Substitute.For<ICakeLog>(), world.HostProcessRunner);

        var environment = await resolver.ResolveAsync("linux-x64", ["net10.0"]);

        await Assert.That(environment).IsEmpty();
    }

    [Test]
    [NonWindowsOnly]
    public async Task ResolveAsync_Should_Throw_PlatformNotSupportedException_When_WinX86_On_NonWindows_Host()
    {
        var world = FakeCakeWorld.CreateLinux();
        var resolver = new DotNetRuntimeEnvironment(world.CakeContext, Substitute.For<ICakeLog>(), world.HostProcessRunner);

        var thrown = await Assert.That(async () => await resolver.ResolveAsync("win-x86", ["net10.0"])).Throws<PlatformNotSupportedException>();
        await Assert.That(thrown!.Message).Contains("win-x86");
        await Assert.That(thrown.Message).Contains("non-Windows");
    }

    [Test]
    public async Task Resolver_Should_Implement_IDotNetRuntimeEnvironment_Contract()
    {
        var world = FakeCakeWorld.CreateWindows();
        var resolver = new DotNetRuntimeEnvironment(world.CakeContext, Substitute.For<ICakeLog>(), world.HostProcessRunner);

        await Assert.That(resolver).IsAssignableTo<IDotNetRuntimeEnvironment>();
    }
}
