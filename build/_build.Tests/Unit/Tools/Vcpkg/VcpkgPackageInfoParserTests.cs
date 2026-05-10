using Build.Tests.Fixtures;
using Build.Tools.Vcpkg;
using Cake.Core.IO;

namespace Build.Tests.Unit.Tools.Vcpkg;

public sealed class VcpkgPackageInfoParserTests
{
    [Test]
    public async Task Parse_Should_Return_Package_Info_When_Json_Output_Is_Valid()
    {
        var result = VcpkgPackageInfoParser.ParseInstalledPackageInfo(
            VcpkgPackageInfoFixture.Sdl2ImageWindows,
            packageName: "sdl2-image",
            triplet: "x64-windows-hybrid",
            installedRoot: new DirectoryPath("C:/repo/vcpkg_installed"));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.PackageName).IsEqualTo("sdl2-image");
        await Assert.That(result.Value.Triplet).IsEqualTo("x64-windows-hybrid");
        await Assert.That(result.Value.DeclaredDependencies).Contains("sdl2:x64-windows-hybrid");
        await Assert.That(result.Value.OwnedFiles).Contains("C:/repo/vcpkg_installed/x64-windows-hybrid/bin/SDL2_image.dll");
    }

    [Test]
    public async Task Parse_Should_Return_Error_When_Output_Is_Empty()
    {
        var result = VcpkgPackageInfoParser.ParseInstalledPackageInfo(
            "",
            packageName: "sdl2-image",
            triplet: "x64-windows-hybrid",
            installedRoot: new DirectoryPath("C:/repo/vcpkg_installed"));

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Message).Contains("returned no output", StringComparison.Ordinal);
    }

    [Test]
    public async Task Parse_Should_Return_Error_When_Package_Key_Is_Missing()
    {
        var result = VcpkgPackageInfoParser.ParseInstalledPackageInfo(
            VcpkgPackageInfoFixture.OtherPackageWindows,
            packageName: "sdl2-image",
            triplet: "x64-windows-hybrid",
            installedRoot: new DirectoryPath("C:/repo/vcpkg_installed"));

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Message).Contains("Failed to deserialize or find package info", StringComparison.Ordinal);
    }

    [Test]
    public async Task Parse_Should_Return_Error_When_Json_Is_Invalid()
    {
        var result = VcpkgPackageInfoParser.ParseInstalledPackageInfo(
            VcpkgPackageInfoFixture.InvalidJson,
            packageName: "sdl2-image",
            triplet: "x64-windows-hybrid",
            installedRoot: new DirectoryPath("C:/repo/vcpkg_installed"));

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Message).Contains("Error building dependency closure", StringComparison.Ordinal);
    }
}
