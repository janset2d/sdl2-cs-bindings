using Build.DependencyAnalysis;
using Build.Host.Paths;
using Build.Runtime;
using Build.Targets.Harvest.Services;
using Build.Tests.Fixtures;
using Build.Tests.Fixtures.Seeders;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;
using IoPath = System.IO.Path;

namespace Build.Tests.Unit.Targets.Harvest.Services;

public sealed class BinaryClosureWalkerTests
{
    private readonly FakeCakeWorldV2 _world;
    private readonly IRuntimeScanner _mockScanner;
    private readonly RuntimeProfile _profile;
    private readonly ICakeContext _mockCtx;
    private readonly FakeFileSystem _fakeFs;
    private readonly PathService _pathService;

    public BinaryClosureWalkerTests()
    {
        _world = FakeCakeWorldV2.CreateWindows();
        _mockScanner = Substitute.For<IRuntimeScanner>();
        _profile = RuntimeProfileFixture.CreateWindows();
        _mockCtx = _world.CakeContext;
        _fakeFs = _world.FileSystem;
        _pathService = new PathService(_world.RepoRoot, CreateParsedArguments(), _world.Log);
        _world.WithToolPath("vcpkg.exe", _pathService.VcpkgRoot.CombineWithFilePath("vcpkg.exe"));
    }

    [Test]
    public async Task BuildClosureAsync_Should_Return_Primary_Binaries_From_Package()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        SeedPackageInfo("sdl2-image", ["bin/SDL2_image.dll"], ["sdl2:x64-windows-hybrid"]);
        SeedPackageInfo("sdl2", ["bin/SDL2.dll"], []);

        _mockScanner.ScanAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<FilePath>());

        var walker = new BinaryClosureWalker(_mockScanner, _profile, _mockCtx, _pathService);
        var result = await walker.BuildClosureAsync(manifest);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.PrimaryFiles.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task BuildClosureAsync_Should_Walk_Package_Dependencies_Recursively()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        SeedPackageInfo("sdl2-image", ["bin/SDL2_image.dll"], ["sdl2:x64-windows-hybrid", "zlib:x64-windows-hybrid"]);
        SeedPackageInfo("sdl2", ["bin/SDL2.dll"], []);
        SeedPackageInfo("zlib", ["bin/zlib1.dll"], []);

        _mockScanner.ScanAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<FilePath>());

        var walker = new BinaryClosureWalker(_mockScanner, _profile, _mockCtx, _pathService);
        var result = await walker.BuildClosureAsync(manifest);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Packages).Contains("sdl2-image");
        await Assert.That(result.Value.Packages).Contains("sdl2");
        await Assert.That(result.Value.Packages).Contains("zlib");
        await Assert.That(_world.ProcessInvocations.Any(invocation =>
            invocation.Arguments.Contains("zlib:x64-windows-hybrid", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task BuildClosureAsync_Should_Skip_Vcpkg_Internal_Packages()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        SeedPackageInfo(
            "sdl2-image",
            ["bin/SDL2_image.dll"],
            ["sdl2:x64-windows-hybrid", "vcpkg-cmake:x64-windows-hybrid", "vcpkg-cmake-config:x64-windows-hybrid"]);
        SeedPackageInfo("sdl2", ["bin/SDL2.dll"], []);

        _mockScanner.ScanAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<FilePath>());

        var walker = new BinaryClosureWalker(_mockScanner, _profile, _mockCtx, _pathService);
        var result = await walker.BuildClosureAsync(manifest);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Packages).DoesNotContain("vcpkg-cmake");
        await Assert.That(result.Value.Packages).DoesNotContain("vcpkg-cmake-config");
    }

    [Test]
    public async Task BuildClosureAsync_Should_Exclude_System_Files_From_Runtime_Scan()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        SeedPackageInfo("sdl2-image", ["bin/SDL2_image.dll"], []);

        var scanResult = new HashSet<FilePath>
        {
            new("C:/Windows/System32/kernel32.dll"),
            new("C:/Windows/System32/user32.dll"),
        };

        _mockScanner.ScanAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(scanResult);

        var walker = new BinaryClosureWalker(_mockScanner, _profile, _mockCtx, _pathService);
        var result = await walker.BuildClosureAsync(manifest);

        await Assert.That(result.IsSuccess).IsTrue();

        var nodeFiles = result.Value.Nodes.Select(n => IoPath.GetFileName(n.Path)).ToList();
        await Assert.That(nodeFiles).DoesNotContain("kernel32.dll");
        await Assert.That(nodeFiles).DoesNotContain("user32.dll");
    }

    [Test]
    public async Task BuildClosureAsync_Should_Return_Error_When_Root_Package_Not_Found()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        SeedMissingPackageInfo("sdl2-image");

        var walker = new BinaryClosureWalker(_mockScanner, _profile, _mockCtx, _pathService);
        var result = await walker.BuildClosureAsync(manifest);

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task BuildClosureAsync_Should_Merge_Runtime_Scan_Results_Into_Closure()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        SeedPackageInfo("sdl2-image", ["bin/SDL2_image.dll"], []);

        var runtimeDiscovered = new FilePath("C:/vcpkg_installed/x64-windows-hybrid/bin/extra_dep.dll");
        var scanResult = new HashSet<FilePath> { runtimeDiscovered };

        _mockScanner.ScanAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(scanResult);

        var walker = new BinaryClosureWalker(_mockScanner, _profile, _mockCtx, _pathService);
        var result = await walker.BuildClosureAsync(manifest);

        await Assert.That(result.IsSuccess).IsTrue();

        var allPaths = result.Value.Nodes.Select(n => IoPath.GetFileName(n.Path)).ToList();
        await Assert.That(allPaths).Contains("extra_dep.dll");
    }

    private static ParsedArguments CreateParsedArguments() => new(
        RepoRoot: null,
        Config: "Release",
        VcpkgDir: null,
        VcpkgInstalledDir: null,
        Library: [],
        Rid: "win-x64",
        Dll: [],
        Suffix: null,
        Scope: [],
        ExplicitVersion: [],
        ExplicitVersions: null,
        VersionsFile: null);

    private void SeedPackageInfo(string name, string[] ownedFiles, string[] dependencies)
    {
        var basePath = _pathService.GetVcpkgInstalledDir.Combine("x64-windows-hybrid");
        foreach (var relativePath in ownedFiles)
        {
            _fakeFs.CreateFile(basePath.CombineWithFilePath(relativePath));
        }

        _world.WithVcpkgPackageInfo(name, "x64-windows-hybrid", ownedFiles, dependencies);
    }

    private void SeedMissingPackageInfo(string name)
    {
        _world.WithMissingVcpkgPackageInfo(name, "x64-windows-hybrid");
    }
}
