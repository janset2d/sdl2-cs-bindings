using Build.Targets.NativeSmoke.Services;
using Build.Tests.Fixtures;
using Cake.Core.Diagnostics;
using NSubstitute;

namespace Build.Tests.Unit.Targets.NativeSmoke.Services;

/// <summary>
/// Unit coverage for the platform-guard contract on <see cref="MsvcDevEnvironment"/>.
/// Windows-specific resolution (VSWhere + vcvarsall.bat invocation + env-delta parse) is
/// exercised end-to-end by a host-RID Cake invocation (e.g. <c>--target Harvest</c> or the
/// repo-root <c>tools ci-sim</c> replay) on Windows, not here — the resolver spawns
/// <c>cmd.exe</c> against a real Visual Studio installation, which is outside the scope of
/// a build-host unit test.
/// </summary>
public sealed class MsvcDevEnvironmentTests
{
    [Test]
    [NonWindowsOnly]
    public async Task ResolveAsync_Should_Throw_PlatformNotSupportedException_When_Host_Is_Not_Windows()
    {
        var world = FakeCakeWorld.CreateLinux();
        var resolver = new MsvcDevEnvironment(world.CakeContext, Substitute.For<ICakeLog>());

        var thrown = await Assert.That(async () => await resolver.ResolveAsync(MsvcTargetArch.X64)).Throws<PlatformNotSupportedException>();
        await Assert.That(thrown!.Message).Contains("Windows-only");
        await Assert.That(thrown.Message).Contains("OperatingSystem.IsWindows");
    }
}
