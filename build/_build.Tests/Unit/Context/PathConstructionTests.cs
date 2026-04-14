using Build.Context.Configs;
using Build.Modules;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Context;

public class PathConstructionTests
{
    private static PathService CreatePathService(string repoRoot = "/repo")
    {
        var repoConfig = new RepositoryConfiguration(new DirectoryPath(repoRoot));
        var parsedArgs = new ParsedArguments(
            RepoRoot: null,
            Config: "Release",
            VcpkgDir: null,
            VcpkgInstalledDir: null,
            Library: [],
            Rid: "",
            Dll: []);
        var log = Substitute.For<ICakeLog>();
        return new PathService(repoConfig, parsedArgs, log);
    }

    [Test]
    public async Task RepoRoot_Should_Return_Configured_Root()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.RepoRoot.FullPath).IsEqualTo("/repo");
    }

    [Test]
    public async Task BuildDir_Should_Be_Under_Repo_Root()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.BuildDir.FullPath).IsEqualTo("/repo/build");
    }

    [Test]
    public async Task ArtifactsDir_Should_Be_Under_Repo_Root()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.ArtifactsDir.FullPath).IsEqualTo("/repo/artifacts");
    }

    [Test]
    public async Task HarvestOutput_Should_Be_Under_Artifacts()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.HarvestOutput.FullPath).IsEqualTo("/repo/artifacts/harvest_output");
    }

    [Test]
    public async Task GetVcpkgInstalledTripletDir_Should_Include_Triplet()
    {
        var svc = CreatePathService("/repo");
        var dir = svc.GetVcpkgInstalledTripletDir("x64-windows-hybrid");
        await Assert.That(dir.FullPath).IsEqualTo("/repo/vcpkg_installed/x64-windows-hybrid");
    }

    [Test]
    public async Task GetVcpkgInstalledBinDir_Should_End_With_Bin()
    {
        var svc = CreatePathService("/repo");
        var dir = svc.GetVcpkgInstalledBinDir("x64-windows-hybrid");
        await Assert.That(dir.FullPath).IsEqualTo("/repo/vcpkg_installed/x64-windows-hybrid/bin");
    }

    [Test]
    public async Task GetVcpkgInstalledLibDir_Should_End_With_Lib()
    {
        var svc = CreatePathService("/repo");
        var dir = svc.GetVcpkgInstalledLibDir("x64-windows-hybrid");
        await Assert.That(dir.FullPath).IsEqualTo("/repo/vcpkg_installed/x64-windows-hybrid/lib");
    }

    [Test]
    public async Task GetHarvestStageDir_Should_Include_Library_And_Rid()
    {
        var svc = CreatePathService("/repo");
        var dir = svc.GetHarvestStageDir("SDL2", "win-x64");
        await Assert.That(dir.FullPath).IsEqualTo("/repo/artifacts/harvest-staging/SDL2-win-x64");
    }

    [Test]
    public async Task GetHarvestStageNativeDir_Should_Follow_NuGet_Runtimes_Convention()
    {
        var svc = CreatePathService("/repo");
        var dir = svc.GetHarvestStageNativeDir("SDL2", "win-x64");
        await Assert.That(dir.FullPath).IsEqualTo("/repo/artifacts/harvest-staging/SDL2-win-x64/runtimes/win-x64/native");
    }

    [Test]
    public async Task GetManifestFile_Should_Point_To_Build_Directory()
    {
        var svc = CreatePathService("/repo");
        var file = svc.GetManifestFile();
        await Assert.That(file.FullPath).IsEqualTo("/repo/build/manifest.json");
    }

    [Test]
    public async Task GetVcpkgPackageCopyrightFile_Should_Be_Under_Share()
    {
        var svc = CreatePathService("/repo");
        var file = svc.GetVcpkgPackageCopyrightFile("x64-windows-hybrid", "sdl2");
        await Assert.That(file.FullPath).IsEqualTo("/repo/vcpkg_installed/x64-windows-hybrid/share/sdl2/copyright");
    }
}
