using Build.Context.Configs;
using Build.Infrastructure.Paths;
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
            Source: "local",
            Rid: "",
            Dll: [],
            VersionSource: null,
            Suffix: null,
            Scope: [],
            ExplicitVersion: []);
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
    public async Task PackagesOutput_Should_Be_Under_Artifacts()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.PackagesOutput.FullPath).IsEqualTo("/repo/artifacts/packages");
    }

    [Test]
    public async Task PackageConsumerSmokeOutput_Should_Be_Under_Artifacts()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.PackageConsumerSmokeOutput.FullPath).IsEqualTo("/repo/artifacts/package-consumer-smoke");
    }

    [Test]
    public async Task SmokeTestResultsOutput_Should_Be_Under_Artifacts_TestResults()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.SmokeTestResultsOutput.FullPath).IsEqualTo("/repo/artifacts/test-results/smoke");
    }

    [Test]
    public async Task BuildProjectFile_Should_Point_To_BuildHost_Csproj()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.BuildProjectFile.FullPath).IsEqualTo("/repo/build/_build/Build.csproj");
    }

    [Test]
    public async Task GetLocalPropsFile_Should_Point_To_Build_Msbuild_Override()
    {
        // Slice C.8a: renamed from Janset.Smoke.local.props to Janset.Local.props; broadened
        // from smoke-specific to repo-wide local-feed override (future samples / AST tests
        // ride the same artefact).
        var svc = CreatePathService("/repo");
        await Assert.That(svc.GetLocalPropsFile().FullPath).IsEqualTo("/repo/build/msbuild/Janset.Local.props");
    }

    [Test]
    public async Task SolutionFile_Should_Point_To_Repo_Solution()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.SolutionFile.FullPath).IsEqualTo("/repo/Janset.SDL2.sln");
    }

    [Test]
    public async Task NativeSmokeProjectDir_Should_Point_To_NativeSmoke_Root()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.NativeSmokeProjectDir.FullPath).IsEqualTo("/repo/tests/smoke-tests/native-smoke");
    }

    [Test]
    public async Task GetNativeSmokeBuildPresetDir_Should_Compose_From_Preset()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.GetNativeSmokeBuildPresetDir("win-x64").FullPath).IsEqualTo("/repo/tests/smoke-tests/native-smoke/build/win-x64");
    }

    [Test]
    public async Task GetNativeSmokeExecutableFile_Should_Use_Windows_Extension_For_Win_Presets()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.GetNativeSmokeExecutableFile("win-x64").FullPath)
            .IsEqualTo("/repo/tests/smoke-tests/native-smoke/build/win-x64/native-smoke.exe");
    }

    [Test]
    public async Task GetNativeSmokeExecutableFile_Should_Use_Unix_Name_For_NonWindows_Presets()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.GetNativeSmokeExecutableFile("linux-x64").FullPath)
            .IsEqualTo("/repo/tests/smoke-tests/native-smoke/build/linux-x64/native-smoke");
    }

    [Test]
    public async Task GetInspectOutputLibraryDir_Should_Compose_Rid_And_Library()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.GetInspectOutputLibraryDir("linux-x64", "SDL2_image").FullPath)
            .IsEqualTo("/repo/artifacts/temp/inspect/linux-x64/SDL2_image");
    }

    [Test]
    public async Task GetMatrixOutputFile_Should_Point_To_Artifacts_Matrix_File()
    {
        var svc = CreatePathService("/repo");
        await Assert.That(svc.GetMatrixOutputFile().FullPath).IsEqualTo("/repo/artifacts/matrix/runtimes.json");
    }

    [Test]
    public async Task GetPackageOutputFile_Should_Compose_Id_And_Version()
    {
        var svc = CreatePathService("/repo");
        var packageFile = svc.GetPackageOutputFile("Janset.SDL2.Core", "2.32.0-local.1");
        await Assert.That(packageFile.FullPath).IsEqualTo("/repo/artifacts/packages/Janset.SDL2.Core.2.32.0-local.1.nupkg");
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
