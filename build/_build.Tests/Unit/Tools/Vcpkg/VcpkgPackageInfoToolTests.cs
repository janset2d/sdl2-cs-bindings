using Build.Tests.Fixtures;
using Build.Tools.Vcpkg;
using Build.Tools.Vcpkg.Settings;
using Cake.Core.IO;

namespace Build.Tests.Unit.Tools.Vcpkg;

public sealed class VcpkgPackageInfoToolTests
{
    [Test]
    public async Task GetPackageInfoJson_Should_Return_Output_And_Include_Expected_Arguments()
    {
        var vcpkgRoot = new DirectoryPath("C:/repo/vcpkg");
        var vcpkgExe = vcpkgRoot.CombineWithFilePath("vcpkg.exe");

        var world = FakeCakeWorld.CreateWindows()
            .WithBinaryFile(vcpkgExe, [])
            .WithToolPath(vcpkgExe)
            .WithProcessResult("vcpkg.exe", exitCode: 0, stdOut: VcpkgPackageInfoFixture.EmptyResults);
        var tool = new VcpkgPackageInfoTool(world.CakeContext);

        var settings = new VcpkgPackageInfoSettings(vcpkgRoot)
        {
            Triplet = "x64-windows-hybrid",
            Installed = true,
            Transitive = true,
            JsonOutput = true,
        };

        var output = tool.GetPackageInfoJson(settings, "sdl2-image:x64-windows-hybrid");

        await Assert.That(output).IsEqualTo(VcpkgPackageInfoFixture.EmptyResults.TrimEnd());

        var renderedArgs = world.ProcessInvocations.Single().Arguments;
        await Assert.That(renderedArgs).Contains("x-package-info");
        await Assert.That(renderedArgs).Contains("sdl2-image:x64-windows-hybrid");
        await Assert.That(renderedArgs).Contains("--triplet");
        await Assert.That(renderedArgs).Contains("x64-windows-hybrid");
        await Assert.That(renderedArgs).Contains("--x-installed");
        await Assert.That(renderedArgs).Contains("--x-transitive");
        await Assert.That(renderedArgs).Contains("--x-json");
    }

    [Test]
    public async Task GetPackageInfoJson_Should_Return_Output_When_Process_Exits_NonZero_But_Has_Stdout()
    {
        var vcpkgRoot = new DirectoryPath("C:/repo/vcpkg");
        var vcpkgExe = vcpkgRoot.CombineWithFilePath("vcpkg.exe");

        var world = FakeCakeWorld.CreateWindows()
            .WithBinaryFile(vcpkgExe, [])
            .WithToolPath(vcpkgExe)
            .WithProcessResult("vcpkg.exe", exitCode: 1, stdOut: VcpkgPackageInfoFixture.Sdl2MinimalWindows);
        var tool = new VcpkgPackageInfoTool(world.CakeContext);

        var settings = new VcpkgPackageInfoSettings(vcpkgRoot)
        {
            JsonOutput = true,
            Installed = true,
        };

        var output = tool.GetPackageInfoJson(settings, "sdl2:x64-windows-hybrid");

        await Assert.That(output).IsEqualTo(VcpkgPackageInfoFixture.Sdl2MinimalWindows.TrimEnd());
    }

    [Test]
    public async Task GetPackageInfoJson_Should_Return_Null_When_Command_Produces_No_Output()
    {
        var vcpkgRoot = new DirectoryPath("C:/repo/vcpkg");
        var vcpkgExe = vcpkgRoot.CombineWithFilePath("vcpkg.exe");

        var world = FakeCakeWorld.CreateWindows()
            .WithBinaryFile(vcpkgExe, [])
            .WithToolPath(vcpkgExe)
            .WithProcessResult("vcpkg.exe", exitCode: 0, stdOut: "");
        var tool = new VcpkgPackageInfoTool(world.CakeContext);

        var settings = new VcpkgPackageInfoSettings(vcpkgRoot)
        {
            JsonOutput = true,
        };

        var output = tool.GetPackageInfoJson(settings, "sdl2:x64-windows-hybrid");

        await Assert.That(output).IsNull();
    }
}
