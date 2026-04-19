using System.Collections.Immutable;
using Build.Application.Harvesting;
using Build.Domain.Harvesting.Models;
using Build.Domain.Harvesting.Results;
using Build.Domain.Runtime;
using Build.Infrastructure.DependencyAnalysis;
using Build.Infrastructure.Vcpkg;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;

namespace Build.Tests.Unit.Application.Harvesting;

public class ClosureBuildingTests
{
    private IRuntimeScanner _mockScanner = null!;
    private IPackageInfoProvider _mockPkg = null!;
    private RuntimeProfile _profile = null!;
    private ICakeContext _mockCtx = null!;
    private FakeFileSystem _fakeFs = null!;

    [Before(Test)]
    public void SetUp()
    {
        _mockScanner = Substitute.For<IRuntimeScanner>();
        _mockPkg = Substitute.For<IPackageInfoProvider>();
        _profile = RuntimeProfileFixture.CreateWindows();

        var env = FakeEnvironment.CreateWindowsEnvironment();
        _fakeFs = new FakeFileSystem(env);
        _mockCtx = Substitute.For<ICakeContext>();
        _mockCtx.Log.Returns(new FakeLog());
        _mockCtx.Environment.Returns(env);
        _mockCtx.FileSystem.Returns(_fakeFs);
    }

    [Test]
    public async Task BuildClosureAsync_Should_Return_Primary_Binaries_From_Package()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        _mockPkg.GetPackageInfoAsync("sdl2-image", "x64-windows-hybrid", Arg.Any<CancellationToken>())
            .Returns(CreatePackageInfo("sdl2-image", ["bin/SDL2_image.dll"], ["sdl2:x64-windows-hybrid"]));

        _mockPkg.GetPackageInfoAsync("sdl2", "x64-windows-hybrid", Arg.Any<CancellationToken>())
            .Returns(CreatePackageInfo("sdl2", ["bin/SDL2.dll"], []));

        _mockScanner.ScanAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableHashSet<FilePath>.Empty);

        var walker = new BinaryClosureWalker(_mockScanner, _mockPkg, _profile, _mockCtx);
        var result = await walker.BuildClosureAsync(manifest);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Closure.PrimaryFiles.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task BuildClosureAsync_Should_Walk_Package_Dependencies_Recursively()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        // sdl2-image depends on sdl2 and zlib
        _mockPkg.GetPackageInfoAsync("sdl2-image", "x64-windows-hybrid", Arg.Any<CancellationToken>())
            .Returns(CreatePackageInfo("sdl2-image", ["bin/SDL2_image.dll"], ["sdl2:x64-windows-hybrid", "zlib:x64-windows-hybrid"]));

        _mockPkg.GetPackageInfoAsync("sdl2", "x64-windows-hybrid", Arg.Any<CancellationToken>())
            .Returns(CreatePackageInfo("sdl2", ["bin/SDL2.dll"], []));

        _mockPkg.GetPackageInfoAsync("zlib", "x64-windows-hybrid", Arg.Any<CancellationToken>())
            .Returns(CreatePackageInfo("zlib", ["bin/zlib1.dll"], []));

        _mockScanner.ScanAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableHashSet<FilePath>.Empty);

        var walker = new BinaryClosureWalker(_mockScanner, _mockPkg, _profile, _mockCtx);
        var result = await walker.BuildClosureAsync(manifest);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Closure.Packages).Contains("sdl2-image");
        await Assert.That(result.Closure.Packages).Contains("sdl2");
        await Assert.That(result.Closure.Packages).Contains("zlib");
    }

    [Test]
    public async Task BuildClosureAsync_Should_Skip_Vcpkg_Internal_Packages()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        // vcpkg-cmake and vcpkg-cmake-config are internal, should be skipped
        _mockPkg.GetPackageInfoAsync("sdl2-image", "x64-windows-hybrid", Arg.Any<CancellationToken>())
            .Returns(CreatePackageInfo("sdl2-image", ["bin/SDL2_image.dll"],
                ["sdl2:x64-windows-hybrid", "vcpkg-cmake:x64-windows-hybrid", "vcpkg-cmake-config:x64-windows-hybrid"]));

        _mockPkg.GetPackageInfoAsync("sdl2", "x64-windows-hybrid", Arg.Any<CancellationToken>())
            .Returns(CreatePackageInfo("sdl2", ["bin/SDL2.dll"], []));

        _mockScanner.ScanAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(ImmutableHashSet<FilePath>.Empty);

        var walker = new BinaryClosureWalker(_mockScanner, _mockPkg, _profile, _mockCtx);
        var result = await walker.BuildClosureAsync(manifest);

        await Assert.That(result.IsSuccess()).IsTrue();
        // vcpkg internal packages should not appear in the processed packages
        await Assert.That(result.Closure.Packages).DoesNotContain("vcpkg-cmake");
        await Assert.That(result.Closure.Packages).DoesNotContain("vcpkg-cmake-config");
    }

    [Test]
    public async Task BuildClosureAsync_Should_Exclude_System_Files_From_Runtime_Scan()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        _mockPkg.GetPackageInfoAsync("sdl2-image", "x64-windows-hybrid", Arg.Any<CancellationToken>())
            .Returns(CreatePackageInfo("sdl2-image", ["bin/SDL2_image.dll"], []));

        // Runtime scan discovers system DLLs
        var scanResult = new HashSet<FilePath>
        {
            new("C:/Windows/System32/kernel32.dll"),
            new("C:/Windows/System32/user32.dll"),
        }.ToImmutableHashSet();

        _mockScanner.ScanAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(scanResult);

        var walker = new BinaryClosureWalker(_mockScanner, _mockPkg, _profile, _mockCtx);
        var result = await walker.BuildClosureAsync(manifest);

        await Assert.That(result.IsSuccess()).IsTrue();

        // System files should NOT appear as nodes in the closure
        var nodeFiles = result.Closure.Nodes.Select(n => n.Path.GetFilename().FullPath).ToList();
        await Assert.That(nodeFiles).DoesNotContain("kernel32.dll");
        await Assert.That(nodeFiles).DoesNotContain("user32.dll");
    }

    [Test]
    public async Task BuildClosureAsync_Should_Return_Error_When_Root_Package_Not_Found()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        _mockPkg.GetPackageInfoAsync("sdl2-image", "x64-windows-hybrid", Arg.Any<CancellationToken>())
            .Returns(new PackageInfoError("Package not found"));

        var walker = new BinaryClosureWalker(_mockScanner, _mockPkg, _profile, _mockCtx);
        var result = await walker.BuildClosureAsync(manifest);

        await Assert.That(result.IsError()).IsTrue();
    }

    [Test]
    public async Task BuildClosureAsync_Should_Merge_Runtime_Scan_Results_Into_Closure()
    {
        var manifest = ManifestFixture.CreateTestSatelliteLibrary();

        _mockPkg.GetPackageInfoAsync("sdl2-image", "x64-windows-hybrid", Arg.Any<CancellationToken>())
            .Returns(CreatePackageInfo("sdl2-image", ["bin/SDL2_image.dll"], []));

        // Runtime scan discovers a DLL not in vcpkg metadata
        var runtimeDiscovered = new FilePath("C:/vcpkg_installed/x64-windows-hybrid/bin/extra_dep.dll");
        var scanResult = new HashSet<FilePath> { runtimeDiscovered }.ToImmutableHashSet();

        _mockScanner.ScanAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(scanResult);

        var walker = new BinaryClosureWalker(_mockScanner, _mockPkg, _profile, _mockCtx);
        var result = await walker.BuildClosureAsync(manifest);

        await Assert.That(result.IsSuccess()).IsTrue();

        var allPaths = result.Closure.Nodes.Select(n => n.Path.GetFilename().FullPath).ToList();
        await Assert.That(allPaths).Contains("extra_dep.dll");
    }

    private PackageInfoResult CreatePackageInfo(string name, string[] ownedFiles, string[] dependencies)
    {
        var basePath = "C:/vcpkg_installed/x64-windows-hybrid";
        var files = ownedFiles.Select(f =>
        {
            var filePath = new FilePath($"{basePath}/{f}");
            _fakeFs.CreateFile(filePath);
            return filePath;
        }).ToImmutableList();
        return new PackageInfo(name, "x64-windows-hybrid", files, dependencies.ToImmutableList());
    }
}
