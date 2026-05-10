using Build.Tests.Fixtures;
using Build.Tools.Vcpkg;
using Build.Tools.Vcpkg.Settings;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Tests.Unit.Tools.Vcpkg;

public sealed class VcpkgAliasesTests
{
    [Test]
    public async Task VcpkgBootstrap_Should_Run_Cmd_On_Windows()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithProcessResult("cmd", "/c \"C:/repo/vcpkg/bootstrap-vcpkg.bat\"", exitCode: 0, stdOut: "bootstrapped");

        world.CakeContext.VcpkgBootstrap(new VcpkgBootstrapSettings
        {
            VcpkgRoot = world.RepoRoot.Combine("vcpkg"),
            WindowsScript = world.RepoRoot.CombineWithFilePath("vcpkg/bootstrap-vcpkg.bat"),
            UnixScript = world.RepoRoot.CombineWithFilePath("vcpkg/bootstrap-vcpkg.sh"),
        });

        var invocation = world.ProcessInvocations.Single();
        await Assert.That(invocation.Command.GetFilename().FullPath).IsEqualTo("cmd");
        await Assert.That(invocation.Arguments).IsEqualTo("/c \"C:/repo/vcpkg/bootstrap-vcpkg.bat\"");
    }

    [Test]
    public async Task VcpkgBootstrap_Should_Run_Bash_On_Unix()
    {
        var world = FakeCakeWorldV2.CreateLinux()
            .WithProcessResult("bash", "\"/repo/vcpkg/bootstrap-vcpkg.sh\"", exitCode: 0, stdOut: "bootstrapped");

        world.CakeContext.VcpkgBootstrap(new VcpkgBootstrapSettings
        {
            VcpkgRoot = world.RepoRoot.Combine("vcpkg"),
            WindowsScript = world.RepoRoot.CombineWithFilePath("vcpkg/bootstrap-vcpkg.bat"),
            UnixScript = world.RepoRoot.CombineWithFilePath("vcpkg/bootstrap-vcpkg.sh"),
        });

        var invocation = world.ProcessInvocations.Single();
        await Assert.That(invocation.Command.GetFilename().FullPath).IsEqualTo("bash");
        await Assert.That(invocation.Arguments).IsEqualTo("\"/repo/vcpkg/bootstrap-vcpkg.sh\"");
    }

    [Test]
    public async Task VcpkgBootstrap_Should_Throw_CakeException_When_Process_Fails()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithProcessResult("cmd", "/c \"C:/repo/vcpkg/bootstrap-vcpkg.bat\"", exitCode: 17, stdOut: "out", stdErr: "err");

        var exception = await Assert.ThrowsAsync<CakeException>(() =>
        {
            world.CakeContext.VcpkgBootstrap(new VcpkgBootstrapSettings
            {
                VcpkgRoot = world.RepoRoot.Combine("vcpkg"),
                WindowsScript = world.RepoRoot.CombineWithFilePath("vcpkg/bootstrap-vcpkg.bat"),
                UnixScript = world.RepoRoot.CombineWithFilePath("vcpkg/bootstrap-vcpkg.sh"),
            });

            return Task.CompletedTask;
        });

        await Assert.That(exception!.Message).Contains("vcpkg bootstrap (Windows) failed with exit code 17", StringComparison.Ordinal);
        await Assert.That(exception.Message).Contains("out", StringComparison.Ordinal);
        await Assert.That(exception.Message).Contains("err", StringComparison.Ordinal);
    }

    [Test]
    public async Task VcpkgPackageInfo_Should_Return_Typed_Info_From_Raw_Json_Output()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithToolPath("vcpkg.exe", new FilePath("C:/repo/vcpkg/vcpkg.exe"))
            .WithProcessResult(
                "vcpkg.exe",
                "x-package-info \"sdl2-image:x64-windows-hybrid\" --x-installed --x-json",
                exitCode: 0,
                stdOut: VcpkgPackageInfoFixture.Sdl2ImageWindows);

        var result = world.CakeContext.VcpkgPackageInfo(
            packageName: "sdl2-image",
            triplet: "x64-windows-hybrid",
            installedRoot: world.RepoRoot.Combine("vcpkg_installed"),
            settings: new VcpkgPackageInfoSettings(world.RepoRoot.Combine("vcpkg")) { Installed = true, JsonOutput = true });

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.PackageName).IsEqualTo("sdl2-image");
        await Assert.That(result.Value.OwnedFiles).Contains("C:/repo/vcpkg_installed/x64-windows-hybrid/bin/SDL2_image.dll");
    }

    [Test]
    public async Task VcpkgPackageInfo_Should_Force_Installed_Json_And_Preserve_Transitive_Settings()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithToolPath("vcpkg.exe", new FilePath("C:/repo/vcpkg/vcpkg.exe"))
            .WithProcessResult(
                "vcpkg.exe",
                "x-package-info \"sdl2-image:x64-windows-hybrid\" --x-installed --x-transitive --x-json",
                exitCode: 0,
                stdOut: VcpkgPackageInfoFixture.Sdl2ImageWindows);

        var result = world.CakeContext.VcpkgPackageInfo(
            packageName: "sdl2-image",
            triplet: "x64-windows-hybrid",
            installedRoot: world.RepoRoot.Combine("vcpkg_installed"),
            settings: new VcpkgPackageInfoSettings(world.RepoRoot.Combine("vcpkg"))
            {
                Installed = false,
                JsonOutput = false,
                Transitive = true,
            });

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.OwnedFiles).Contains("C:/repo/vcpkg_installed/x64-windows-hybrid/bin/SDL2_image.dll");
    }
}
