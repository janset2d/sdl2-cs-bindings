using Build.Host.Paths;
using Build.Host.Runtime;
using Build.Tests.Fixtures;
using Build.Validation.Harvesting;
using NSubstitute;

namespace Build.Tests.Unit.Validation.Harvesting;

public sealed class HarvestPreconditionsValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Empty_Report_When_Vcpkg_Triplet_Dir_Exists()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithTextFile("vcpkg_installed/x64-windows-hybrid/.placeholder", string.Empty);

        var pathService = Substitute.For<IPathService>();
        pathService.GetVcpkgInstalledTripletDir("x64-windows-hybrid")
            .Returns(world.RepoRoot.Combine("vcpkg_installed/x64-windows-hybrid"));

        var profile = Substitute.For<IRuntimeProfile>();
        profile.Triplet.Returns("x64-windows-hybrid");
        profile.Rid.Returns("win-x64");

        var validator = new HarvestPreconditionsValidator(world.CakeContext, pathService, profile);
        var report = validator.Validate();

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Fail_When_Vcpkg_Triplet_Dir_Missing()
    {
        var world = FakeCakeWorld.CreateWindows();

        var pathService = Substitute.For<IPathService>();
        pathService.GetVcpkgInstalledTripletDir("x64-windows-hybrid")
            .Returns(world.RepoRoot.Combine("vcpkg_installed/x64-windows-hybrid"));

        var profile = Substitute.For<IRuntimeProfile>();
        profile.Triplet.Returns("x64-windows-hybrid");
        profile.Rid.Returns("win-x64");

        var validator = new HarvestPreconditionsValidator(world.CakeContext, pathService, profile);
        var report = validator.Validate();

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Severity).IsEqualTo(Build.Results.ValidationSeverity.Error);
        await Assert.That(report.Errors[0].Message).Contains("vcpkg triplet directory", StringComparison.Ordinal);
        await Assert.That(report.Errors[0].Message).Contains("EnsureVcpkgDependencies", StringComparison.Ordinal);
        await Assert.That(report.Errors[0].Message).Contains("--rid win-x64", StringComparison.Ordinal);
    }

    [Test]
    public async Task Validate_Should_Throw_ArgumentNullException_When_Constructor_Args_Null()
    {
        var world = FakeCakeWorld.CreateWindows();
        var pathService = Substitute.For<IPathService>();
        var profile = Substitute.For<IRuntimeProfile>();

        await Assert.That(() => new HarvestPreconditionsValidator(null!, pathService, profile))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new HarvestPreconditionsValidator(world.CakeContext, null!, profile))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new HarvestPreconditionsValidator(world.CakeContext, pathService, null!))
            .Throws<ArgumentNullException>();
    }
}
