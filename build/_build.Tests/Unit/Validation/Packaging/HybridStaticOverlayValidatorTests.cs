using System.Collections.Immutable;
using Build.Data.Manifest.Models;
using Build.Host.Paths;
using Build.Tests.Fixtures;
using Build.Validation.Packaging;
using NSubstitute;

namespace Build.Tests.Unit.Validation.Packaging;

public sealed class HybridStaticOverlayValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Empty_Report_When_All_Triplets_Have_Hybrid_Suffix_And_Overlay_Files_Exist()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# fake overlay");
        var pathService = CreatePathServiceFor(world);

        var runtimes = ImmutableList.Create(
            new RuntimeInfo { Rid = "win-x64", Triplet = "x64-windows-hybrid", Runner = "windows-2025-vs2026", ContainerImage = null });

        var validator = new HybridStaticOverlayValidator(world.CakeContext, pathService);
        var report = validator.Validate(runtimes);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Fail_When_Triplet_Lacks_Hybrid_Suffix()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var pathService = CreatePathServiceFor(world);

        var runtimes = ImmutableList.Create(
            new RuntimeInfo { Rid = "win-x64", Triplet = "x64-windows-stock", Runner = "windows-2025-vs2026", ContainerImage = null });

        var validator = new HybridStaticOverlayValidator(world.CakeContext, pathService);
        var report = validator.Validate(runtimes);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Code).IsEqualTo("G16");
        await Assert.That(report.Errors[0].Message).Contains("does not end with '-hybrid'", StringComparison.Ordinal);
    }

    [Test]
    public async Task Validate_Should_Fail_When_Hybrid_Triplet_Has_No_Overlay_File()
    {
        // Hybrid suffix but no overlay file seeded — operational drift case.
        var world = FakeCakeWorldV2.CreateWindows();
        var pathService = CreatePathServiceFor(world);

        var runtimes = ImmutableList.Create(
            new RuntimeInfo { Rid = "win-x64", Triplet = "x64-windows-hybrid", Runner = "windows-2025-vs2026", ContainerImage = null });

        var validator = new HybridStaticOverlayValidator(world.CakeContext, pathService);
        var report = validator.Validate(runtimes);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Code).IsEqualTo("G16");
        await Assert.That(report.Errors[0].Message).Contains("does not exist", StringComparison.Ordinal);
    }

    [Test]
    public async Task Validate_Should_Aggregate_Multiple_Violations_Across_Runtimes()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# fake overlay");
        var pathService = CreatePathServiceFor(world);

        var runtimes = ImmutableList.Create(
            new RuntimeInfo { Rid = "win-x64", Triplet = "x64-windows-hybrid", Runner = "windows-2025-vs2026", ContainerImage = null },
            new RuntimeInfo { Rid = "linux-x64", Triplet = "x64-linux-stock", Runner = "ubuntu-24.04", ContainerImage = null },
            new RuntimeInfo { Rid = "osx-x64", Triplet = "x64-osx-hybrid", Runner = "macos-15-intel", ContainerImage = null });

        var validator = new HybridStaticOverlayValidator(world.CakeContext, pathService);
        var report = validator.Validate(runtimes);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(2);
        await Assert.That(report.Errors.All(e => string.Equals(e.Code, "G16", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Return_Empty_Report_When_Runtimes_Empty()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var pathService = CreatePathServiceFor(world);

        var validator = new HybridStaticOverlayValidator(world.CakeContext, pathService);
        var report = validator.Validate(ImmutableList<RuntimeInfo>.Empty);

        await Assert.That(report.Count).IsEqualTo(0);
    }

    private static IPathService CreatePathServiceFor(FakeCakeWorldV2 world)
    {
        var pathService = Substitute.For<IPathService>();
        pathService.VcpkgOverlayTripletsDir.Returns(world.RepoRoot.Combine("vcpkg-overlay-triplets"));
        return pathService;
    }
}
