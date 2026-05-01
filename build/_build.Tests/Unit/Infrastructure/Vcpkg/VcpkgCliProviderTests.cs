using Build.Domain.Harvesting.Results;
using Build.Domain.Paths;
using Build.Infrastructure.Vcpkg;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;

namespace Build.Tests.Unit.Infrastructure.Vcpkg;

public sealed class VcpkgCliProviderTests
{
    [Test]
    public async Task GetPackageInfoAsync_Should_Return_PackageInfo_When_Json_Output_Is_Valid()
    {
        var packageKey = "sdl2-image:x64-windows-hybrid";
        var provider = CreateProvider(
            [
                "{",
                "  \"results\": {",
                $"    \"{packageKey}\": {{",
                "      \"version-string\": \"2.8.8\",",
                "      \"port-version\": 2,",
                "      \"triplet\": \"x64-windows-hybrid\",",
                "      \"dependencies\": [\"sdl2:x64-windows-hybrid\", \"libpng:x64-windows-hybrid\"],",
                "      \"owns\": [\"x64-windows-hybrid/bin/SDL2_image.dll\", \"x64-windows-hybrid/share/sdl2-image/copyright\"]",
                "    }",
                "  }",
                "}",
            ]);

        var result = await provider.GetPackageInfoAsync("sdl2-image", "x64-windows-hybrid");

        await Assert.That(result.IsSuccess()).IsTrue();

        var packageInfo = PackageInfoResult.ToPackageInfo(result);
        await Assert.That(packageInfo.PackageName).IsEqualTo("sdl2-image");
        await Assert.That(packageInfo.Triplet).IsEqualTo("x64-windows-hybrid");
        await Assert.That(packageInfo.DeclaredDependencies).Contains("sdl2:x64-windows-hybrid");
        await Assert.That(packageInfo.OwnedFiles.Select(path => path.FullPath)).Contains("C:/repo/vcpkg_installed/x64-windows-hybrid/bin/SDL2_image.dll");
    }

    [Test]
    public async Task GetPackageInfoAsync_Should_Return_Error_When_Command_Returns_No_Output()
    {
        var provider = CreateProvider([]);

        var result = await provider.GetPackageInfoAsync("sdl2-image", "x64-windows-hybrid");

        await Assert.That(result.IsError()).IsTrue();

        var error = PackageInfoResult.ToPackageInfoError(result);
        await Assert.That(error.Message).Contains("returned no output");
    }

    [Test]
    public async Task GetPackageInfoAsync_Should_Return_Error_When_Package_Key_Is_Missing_From_Json()
    {
        var provider = CreateProvider(
        [
            "{",
            "  \"results\": {",
            "    \"other-package:x64-windows-hybrid\": {",
            "      \"version-string\": \"1.0.0\",",
            "      \"port-version\": 0,",
            "      \"triplet\": \"x64-windows-hybrid\",",
            "      \"owns\": [\"x64-windows-hybrid/bin/other.dll\"]",
            "    }",
            "  }",
            "}",
        ]);

        var result = await provider.GetPackageInfoAsync("sdl2-image", "x64-windows-hybrid");

        await Assert.That(result.IsError()).IsTrue();

        var error = PackageInfoResult.ToPackageInfoError(result);
        await Assert.That(error.Message).Contains("Failed to deserialize or find package info");
    }

    [Test]
    public async Task GetPackageInfoAsync_Should_Return_Error_When_Json_Is_Invalid()
    {
        var provider = CreateProvider(["this is not json"]);

        var result = await provider.GetPackageInfoAsync("sdl2-image", "x64-windows-hybrid");

        await Assert.That(result.IsError()).IsTrue();

        var error = PackageInfoResult.ToPackageInfoError(result);
        await Assert.That(error.Message).Contains("Error building dependency closure");
    }

    private static VcpkgCliProvider CreateProvider(IReadOnlyList<string> standardOutput)
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var log = new FakeLog();

        var vcpkgRoot = new DirectoryPath("C:/repo/vcpkg");
        fileSystem.CreateDirectory(vcpkgRoot);
        fileSystem.CreateFile(vcpkgRoot.CombineWithFilePath("vcpkg.exe"));

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithToolPath(vcpkgRoot.CombineWithFilePath("vcpkg.exe"))
            .WithStandardOutput(standardOutput)
            .Build();

        var pathService = Substitute.For<IPathService>();
        pathService.VcpkgRoot.Returns(vcpkgRoot);
        pathService.GetVcpkgInstalledDir.Returns(new DirectoryPath("C:/repo/vcpkg_installed"));

        return new VcpkgCliProvider(context, pathService, log);
    }
}
