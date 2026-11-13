using Build.Tests.Fixtures;
using Build.Tools.Vcpkg;

namespace Build.Tests.Unit.Tools.Vcpkg;

public sealed class VcpkgInstallToolTests
{
    [Test]
    [MethodDataSource(typeof(VcpkgInstallData), nameof(VcpkgInstallData.MissingPackages))]
    public async Task Install_Should_Reject_Missing_Packages_In_Classic_Mode(IReadOnlyList<string>? packages)
    {
        var world = VcpkgInstallData.CreateWorld();
        var tool = new VcpkgInstallTool(world.CakeContext);

        var exception = await Assert.That(() => tool.Install(VcpkgInstallData.AllSwitches(), packages))
            .ThrowsExactly<ArgumentException>();

        await Assert.That(exception!.ParamName).IsEqualTo("packages");
    }

    [Test]
    public async Task Install_Should_Render_All_Switches_In_Order()
    {
        var world = VcpkgInstallData.CreateWorld();
        var tool = new VcpkgInstallTool(world.CakeContext);

        tool.Install(VcpkgInstallData.AllSwitches(), ["sdl2", "sdl2-image"]);

        await Assert.That(world.ProcessInvocations.Single().Arguments).IsEqualTo(VcpkgInstallData.ExpectedArguments);
    }
}
