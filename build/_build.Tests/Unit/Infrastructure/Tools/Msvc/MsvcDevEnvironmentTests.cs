using Build.Domain.Runtime;
using Build.Infrastructure.Tools.Msvc;
using Build.Tests.Fixtures;
using Cake.Core.Diagnostics;
using NSubstitute;

namespace Build.Tests.Unit.Infrastructure.Tools.Msvc;

/// <summary>
/// Unit coverage for the platform-guard + contract shape. Windows-specific resolution
/// (VSWhere + vcvarsall.bat invocation + env-delta parse) is exercised end-to-end by
/// <c>tests/scripts/smoke-witness.cs ci-sim</c> on Windows, not by this fixture — the
/// resolver spawns <c>cmd.exe</c> and a real Visual Studio installation, which is
/// outside the scope of a build-host unit test.
/// </summary>
public sealed class MsvcDevEnvironmentTests
{
    [Test]
    public async Task ResolveAsync_Should_Throw_PlatformNotSupportedException_When_Host_Is_Not_Windows()
    {
        if (OperatingSystem.IsWindows())
        {
            // The contract is Windows-only; on Windows the resolver enters its real
            // discovery path and this assertion cannot be exercised without stubbing
            // OperatingSystem.IsWindows (static; not mockable). Leave the test active
            // so non-Windows CI runners (Linux matrix, macOS matrix) pin the contract.
            return;
        }

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Unix).BuildContextWithHandles();
        var resolver = new MsvcDevEnvironment(repo.CakeContext, Substitute.For<ICakeLog>());

        var thrown = await Assert.That(async () => await resolver.ResolveAsync()).Throws<PlatformNotSupportedException>();
        await Assert.That(thrown!.Message).Contains("Windows-only");
        await Assert.That(thrown.Message).Contains("OperatingSystem.IsWindows");
    }

    [Test]
    public async Task Resolver_Should_Implement_IMsvcDevEnvironment_Contract()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var resolver = new MsvcDevEnvironment(repo.CakeContext, Substitute.For<ICakeLog>());

        await Assert.That(resolver).IsAssignableTo<IMsvcDevEnvironment>();
    }
}
