using Build.Tests.Fixtures;
using Build.Tools.Vcpkg;

namespace Build.Tests.Unit.Tools.Vcpkg;

public sealed class VcpkgInstallToolTests
{
    [Test]
    public async Task Install_Should_Render_All_Switches_In_Order()
    {
        var executable = VcpkgInstallData.Root.CombineWithFilePath("vcpkg.exe");
        var world = FakeCakeWorld.CreateWindows()
            .WithBinaryFile(executable, [])
            .WithToolPath(executable)
            .WithProcessResult("vcpkg.exe", exitCode: 0, stdOut: "");
        var tool = new VcpkgInstallTool(world.CakeContext);

        tool.Install(VcpkgInstallData.AllSwitches(), ["sdl2", "sdl2-image"]);

        await Assert.That(world.ProcessInvocations.Single().Arguments).IsEqualTo(VcpkgInstallData.ExpectedArguments);
    }
}
