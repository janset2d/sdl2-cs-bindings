using Build.Tests.Fixtures;
using Build.Tools.Vcpkg;
using Build.Tools.Vcpkg.Settings;
using Cake.Core.IO;
using Cake.Testing;

namespace Build.Tests.Unit.Tools.Vcpkg;

public sealed class VcpkgPackageInfoToolTests
{
    [Test]
    public async Task GetPackageInfo_Should_Return_Output_And_Include_Expected_Arguments()
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var vcpkgRoot = new DirectoryPath("C:/repo/vcpkg");
        fileSystem.CreateDirectory(vcpkgRoot);

        var vcpkgExe = vcpkgRoot.CombineWithFilePath("vcpkg.exe");
        fileSystem.CreateFile(vcpkgExe);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithProcessCapture(out var capture)
            .WithToolPath(vcpkgExe)
            .WithStandardOutput(["{\"results\":{}}"])
            .Build();
        var tool = new VcpkgPackageInfoTool(context);

        var settings = new VcpkgPackageInfoSettings(vcpkgRoot)
        {
            Triplet = "x64-windows-hybrid",
            Installed = true,
            Transitive = true,
            JsonOutput = true,
        };

        var output = tool.GetPackageInfo(settings, "sdl2-image:x64-windows-hybrid");

        await Assert.That(output).IsEqualTo("{\"results\":{}}");
        await Assert.That(capture.Settings).IsNotNull();

        var renderedArgs = capture.Settings!.Arguments.Render();
        await Assert.That(renderedArgs).Contains("x-package-info");
        await Assert.That(renderedArgs).Contains("sdl2-image:x64-windows-hybrid");
        await Assert.That(renderedArgs).Contains("--triplet");
        await Assert.That(renderedArgs).Contains("x64-windows-hybrid");
        await Assert.That(renderedArgs).Contains("--x-installed");
        await Assert.That(renderedArgs).Contains("--x-transitive");
        await Assert.That(renderedArgs).Contains("--x-json");
    }

    [Test]
    public async Task GetPackageInfo_Should_Return_Output_When_Process_Exits_NonZero_But_Has_Stdout()
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var vcpkgRoot = new DirectoryPath("C:/repo/vcpkg");
        fileSystem.CreateDirectory(vcpkgRoot);

        var vcpkgExe = vcpkgRoot.CombineWithFilePath("vcpkg.exe");
        fileSystem.CreateFile(vcpkgExe);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithProcessCapture(out _)
            .WithToolPath(vcpkgExe)
            .WithExitCode(1)
            .WithStandardOutput(["{\"results\":{\"sdl2\":{}}}"])
            .Build();
        var tool = new VcpkgPackageInfoTool(context);

        var settings = new VcpkgPackageInfoSettings(vcpkgRoot)
        {
            JsonOutput = true,
            Installed = true,
        };

        var output = tool.GetPackageInfo(settings, "sdl2:x64-windows-hybrid");

        await Assert.That(output).IsEqualTo("{\"results\":{\"sdl2\":{}}}");
    }

    [Test]
    public async Task GetPackageInfo_Should_Return_Null_When_Command_Produces_No_Output()
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var vcpkgRoot = new DirectoryPath("C:/repo/vcpkg");
        fileSystem.CreateDirectory(vcpkgRoot);

        var vcpkgExe = vcpkgRoot.CombineWithFilePath("vcpkg.exe");
        fileSystem.CreateFile(vcpkgExe);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithProcessCapture(out _)
            .WithToolPath(vcpkgExe)
            .WithStandardOutput([])
            .Build();
        var tool = new VcpkgPackageInfoTool(context);

        var settings = new VcpkgPackageInfoSettings(vcpkgRoot)
        {
            JsonOutput = true,
        };

        var output = tool.GetPackageInfo(settings, "sdl2:x64-windows-hybrid");

        await Assert.That(output).IsNull();
    }
}
