using System.Collections.Immutable;
using Build.Application.Harvesting;
using Build.Domain.Harvesting.Models;
using Build.Domain.Paths;
using Build.Domain.Runtime;
using Build.Infrastructure.Vcpkg;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;

namespace Build.Tests.Unit.Domain.Harvesting;

public class DeploymentPlanTests
{
    private IPackageInfoProvider _mockPkg = null!;
    private RuntimeProfile _windowsProfile = null!;
    private RuntimeProfile _linuxProfile = null!;
    private IPathService _mockPathService = null!;

    [Before(Test)]
    public void SetUp()
    {
        _mockPkg = Substitute.For<IPackageInfoProvider>();
        _windowsProfile = RuntimeProfileFixture.CreateWindows();
        _linuxProfile = RuntimeProfileFixture.CreateLinux();
        _mockPathService = Substitute.For<IPathService>();
        _mockPathService.GetVcpkgInstalledLibDir(Arg.Any<string>())
            .Returns(new DirectoryPath("/vcpkg_installed/x64-linux-hybrid/lib"));
    }

    [Test]
    public async Task CreatePlanAsync_Should_Create_FileCopyActions_On_Windows()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .Build();

        var ctx = CreateWindowsCakeContext();
        var manifestConfig = ManifestFixture.CreateTestManifestConfig();
        var planner = new ArtifactPlanner(_mockPkg, _windowsProfile, _mockPathService, ctx, manifestConfig);

        SetupEmptyLicenseResponse();

        var result = await planner.CreatePlanAsync(manifest, closure, new DirectoryPath("/output"));

        await Assert.That(result.IsSuccess()).IsTrue();

        var plan = result.AsT1.Value;
        var fileCopyActions = plan.Actions.OfType<FileCopyAction>().ToList();
        await Assert.That(fileCopyActions.Count).IsGreaterThan(0);
        await Assert.That(plan.Statistics.DeploymentStrategy).IsEqualTo(DeploymentStrategy.DirectCopy);
    }

    [Test]
    public async Task CreatePlanAsync_Should_Create_ArchiveAction_On_Linux()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("/vcpkg/lib/libSDL2_image.so", "sdl2-image")
            .Build();

        var ctx = CreateLinuxCakeContext();
        var manifestConfig = ManifestFixture.CreateTestManifestConfig();
        var planner = new ArtifactPlanner(_mockPkg, _linuxProfile, _mockPathService, ctx, manifestConfig);

        SetupEmptyLicenseResponse();

        var result = await planner.CreatePlanAsync(manifest, closure, new DirectoryPath("/output"));

        await Assert.That(result.IsSuccess()).IsTrue();

        var plan = result.AsT1.Value;
        var archiveActions = plan.Actions.OfType<ArchiveCreationAction>().ToList();
        await Assert.That(archiveActions.Count).IsGreaterThan(0);
        await Assert.That(plan.Statistics.DeploymentStrategy).IsEqualTo(DeploymentStrategy.Archive);
    }

    [Test]
    public async Task CreatePlanAsync_Should_Exclude_Core_Deps_From_Satellite_Plans()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        // Closure has both satellite and core library binaries
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/SDL2.dll", "sdl2", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/zlib1.dll", "zlib", "sdl2-image")
            .Build();

        var ctx = CreateWindowsCakeContext();
        var manifestConfig = ManifestFixture.CreateTestManifestConfig();
        var planner = new ArtifactPlanner(_mockPkg, _windowsProfile, _mockPathService, ctx, manifestConfig);

        SetupEmptyLicenseResponse();

        var result = await planner.CreatePlanAsync(manifest, closure, new DirectoryPath("/output"));

        await Assert.That(result.IsSuccess()).IsTrue();

        var plan = result.AsT1.Value;
        // Core library (sdl2) deps should be filtered out for satellite packages
        var deployedFiles = plan.Actions.OfType<FileCopyAction>()
            .Where(a => a.Origin != ArtifactOrigin.License)
            .Select(a => a.SourcePath.GetFilename().FullPath)
            .ToList();

        await Assert.That(deployedFiles).DoesNotContain("SDL2.dll");
        await Assert.That(deployedFiles).Contains("SDL2_image.dll");
    }

    [Test]
    public async Task CreatePlanAsync_Should_Include_All_Deps_For_Core_Library()
    {
        var manifest = ManifestFixture.CreateTestCoreLibrary();

        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2.dll", "sdl2")
            .Build();

        var ctx = CreateWindowsCakeContext();
        var manifestConfig = ManifestFixture.CreateTestManifestConfig();
        var planner = new ArtifactPlanner(_mockPkg, _windowsProfile, _mockPathService, ctx, manifestConfig);

        SetupEmptyLicenseResponse();

        var result = await planner.CreatePlanAsync(manifest, closure, new DirectoryPath("/output"));

        await Assert.That(result.IsSuccess()).IsTrue();

        var plan = result.AsT1.Value;
        var deployedFiles = plan.Actions.OfType<FileCopyAction>()
            .Where(a => a.Origin != ArtifactOrigin.License)
            .Select(a => a.SourcePath.GetFilename().FullPath)
            .ToList();

        await Assert.That(deployedFiles).Contains("SDL2.dll");
    }

    [Test]
    public async Task CreatePlanAsync_Should_Calculate_Correct_Statistics()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/zlib1.dll", "zlib", "sdl2-image")
            .Build();

        var ctx = CreateWindowsCakeContext();
        var manifestConfig = ManifestFixture.CreateTestManifestConfig();
        var planner = new ArtifactPlanner(_mockPkg, _windowsProfile, _mockPathService, ctx, manifestConfig);

        SetupEmptyLicenseResponse();

        var result = await planner.CreatePlanAsync(manifest, closure, new DirectoryPath("/output"));

        await Assert.That(result.IsSuccess()).IsTrue();

        var stats = result.AsT1.Value.Statistics;
        await Assert.That(stats.LibraryName).IsEqualTo("SDL2_image");
        await Assert.That(stats.PrimaryFiles.Count).IsEqualTo(1);
        await Assert.That(stats.RuntimeFiles.Count).IsEqualTo(1);
        await Assert.That(stats.DeployedPackages).Contains("sdl2-image");
        await Assert.That(stats.DeployedPackages).Contains("zlib");
    }

    [Test]
    public async Task CreatePlanAsync_Should_Include_License_Files()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .Build();

        var ctx = CreateWindowsCakeContext();
        var manifestConfig = ManifestFixture.CreateTestManifestConfig();
        var planner = new ArtifactPlanner(_mockPkg, _windowsProfile, _mockPathService, ctx, manifestConfig);

        // Package has a copyright file
        _mockPkg.GetPackageInfoAsync("sdl2-image", "x64-windows-hybrid", Arg.Any<CancellationToken>())
            .Returns(new PackageInfo(
                "sdl2-image", "x64-windows-hybrid",
                ImmutableList.Create<FilePath>(
                    new FilePath("C:/vcpkg/bin/SDL2_image.dll"),
                    new FilePath("C:/vcpkg/share/sdl2-image/copyright")),
                ImmutableList<string>.Empty));

        var result = await planner.CreatePlanAsync(manifest, closure, new DirectoryPath("/output"));

        await Assert.That(result.IsSuccess()).IsTrue();

        var plan = result.AsT1.Value;
        var licenseActions = plan.Actions.OfType<FileCopyAction>()
            .Where(a => a.Origin == ArtifactOrigin.License)
            .ToList();

        await Assert.That(licenseActions.Count).IsGreaterThan(0);
        await Assert.That(plan.Statistics.LicenseFiles.Count).IsGreaterThan(0);
    }

    private static ICakeContext CreateWindowsCakeContext()
    {
        var env = FakeEnvironment.CreateWindowsEnvironment();
        var ctx = Substitute.For<ICakeContext>();
        ctx.Environment.Returns(env);
        ctx.Log.Returns(new FakeLog());
        return ctx;
    }

    private static ICakeContext CreateLinuxCakeContext()
    {
        var env = FakeEnvironment.CreateUnixEnvironment();
        var ctx = Substitute.For<ICakeContext>();
        ctx.Environment.Returns(env);
        ctx.Log.Returns(new FakeLog());
        return ctx;
    }

    private void SetupEmptyLicenseResponse()
    {
        _mockPkg.GetPackageInfoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new PackageInfo(
                callInfo.ArgAt<string>(0), callInfo.ArgAt<string>(1),
                ImmutableList<FilePath>.Empty, ImmutableList<string>.Empty));
    }
}
