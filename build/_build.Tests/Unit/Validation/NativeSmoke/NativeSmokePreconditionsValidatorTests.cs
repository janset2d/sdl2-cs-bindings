using Build.Host.Paths;
using Build.Tests.Fixtures;
using Build.Validation.NativeSmoke;
using NSubstitute;

namespace Build.Tests.Unit.Validation.NativeSmoke;

public sealed class NativeSmokePreconditionsValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Empty_Report_When_Project_Dir_And_Cmake_Files_Exist()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("tests/smoke-tests/native-smoke/CMakeLists.txt", "# fake")
            .WithTextFile("tests/smoke-tests/native-smoke/CMakePresets.json", "{}");

        var pathService = Substitute.For<IPathService>();
        pathService.NativeSmokeProjectDir.Returns(world.RepoRoot.Combine("tests/smoke-tests/native-smoke"));

        var validator = new NativeSmokePreconditionsValidator(world.CakeContext, pathService);
        var report = validator.Validate();

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Fail_When_Project_Dir_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();

        var pathService = Substitute.For<IPathService>();
        pathService.NativeSmokeProjectDir.Returns(world.RepoRoot.Combine("tests/smoke-tests/native-smoke"));

        var validator = new NativeSmokePreconditionsValidator(world.CakeContext, pathService);
        var report = validator.Validate();

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Message).Contains("project directory", StringComparison.Ordinal);
        await Assert.That(report.Errors[0].Message).Contains("Sync the repository checkout", StringComparison.Ordinal);
    }

    [Test]
    public async Task Validate_Should_Fail_When_CMakeLists_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("tests/smoke-tests/native-smoke/CMakePresets.json", "{}");

        var pathService = Substitute.For<IPathService>();
        pathService.NativeSmokeProjectDir.Returns(world.RepoRoot.Combine("tests/smoke-tests/native-smoke"));

        var validator = new NativeSmokePreconditionsValidator(world.CakeContext, pathService);
        var report = validator.Validate();

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Message).Contains("CMakeLists.txt", StringComparison.Ordinal);
    }

    [Test]
    public async Task Validate_Should_Fail_When_CMakePresets_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("tests/smoke-tests/native-smoke/CMakeLists.txt", "# fake");

        var pathService = Substitute.For<IPathService>();
        pathService.NativeSmokeProjectDir.Returns(world.RepoRoot.Combine("tests/smoke-tests/native-smoke"));

        var validator = new NativeSmokePreconditionsValidator(world.CakeContext, pathService);
        var report = validator.Validate();

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Message).Contains("CMakePresets.json", StringComparison.Ordinal);
    }

    [Test]
    public async Task Validate_Should_Aggregate_Multiple_Missing_Files()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("tests/smoke-tests/native-smoke/.placeholder", string.Empty);

        var pathService = Substitute.For<IPathService>();
        pathService.NativeSmokeProjectDir.Returns(world.RepoRoot.Combine("tests/smoke-tests/native-smoke"));

        var validator = new NativeSmokePreconditionsValidator(world.CakeContext, pathService);
        var report = validator.Validate();

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(2);
        await Assert.That(report.Errors.Any(e => e.Message.Contains("CMakeLists.txt", StringComparison.Ordinal))).IsTrue();
        await Assert.That(report.Errors.Any(e => e.Message.Contains("CMakePresets.json", StringComparison.Ordinal))).IsTrue();
    }
}
