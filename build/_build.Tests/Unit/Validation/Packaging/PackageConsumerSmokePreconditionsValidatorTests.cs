using Build.Tests.Fixtures;
using Build.Validation.Packaging;

namespace Build.Tests.Unit.Validation.Packaging;

public sealed class PackageConsumerSmokePreconditionsValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Valid_When_All_Inputs_Present()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        world.WithTextFile("tests/Smoke.csproj", "<Project />");
        world.WithTextFile("tests/CompileSanity.csproj", "<Project />");
        world.WithTextFile("artifacts/packages/.placeholder", "");

        var validator = new PackageConsumerSmokePreconditionsValidator(world.CakeContext);

        var report = validator.Validate(
            world.RepoRoot.CombineWithFilePath("tests/Smoke.csproj"),
            world.RepoRoot.CombineWithFilePath("tests/CompileSanity.csproj"),
            world.RepoRoot.Combine("artifacts/packages"));

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Smoke_Csproj_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        world.WithTextFile("tests/CompileSanity.csproj", "<Project />");
        world.WithTextFile("artifacts/packages/.placeholder", "");

        var validator = new PackageConsumerSmokePreconditionsValidator(world.CakeContext);

        var report = validator.Validate(
            world.RepoRoot.CombineWithFilePath("tests/Smoke.csproj"),
            world.RepoRoot.CombineWithFilePath("tests/CompileSanity.csproj"),
            world.RepoRoot.Combine("artifacts/packages"));

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(e => e.Code == "PCSP-01")).IsTrue();
        await Assert.That(report.Errors.Any(e => e.Message.Contains("smoke project", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Compile_Sanity_Csproj_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        world.WithTextFile("tests/Smoke.csproj", "<Project />");
        world.WithTextFile("artifacts/packages/.placeholder", "");

        var validator = new PackageConsumerSmokePreconditionsValidator(world.CakeContext);

        var report = validator.Validate(
            world.RepoRoot.CombineWithFilePath("tests/Smoke.csproj"),
            world.RepoRoot.CombineWithFilePath("tests/CompileSanity.csproj"),
            world.RepoRoot.Combine("artifacts/packages"));

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(e => e.Code == "PCSP-02")).IsTrue();
        await Assert.That(report.Errors.Any(e => e.Message.Contains("compile-sanity", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Feed_Directory_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        world.WithTextFile("tests/Smoke.csproj", "<Project />");
        world.WithTextFile("tests/CompileSanity.csproj", "<Project />");

        var validator = new PackageConsumerSmokePreconditionsValidator(world.CakeContext);

        var report = validator.Validate(
            world.RepoRoot.CombineWithFilePath("tests/Smoke.csproj"),
            world.RepoRoot.CombineWithFilePath("tests/CompileSanity.csproj"),
            world.RepoRoot.Combine("artifacts/packages"));

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(e => e.Code == "PCSP-03")).IsTrue();
        await Assert.That(report.Errors.Any(e => e.Message.Contains("local feed", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Aggregate_All_Errors_When_Multiple_Inputs_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();

        var validator = new PackageConsumerSmokePreconditionsValidator(world.CakeContext);

        var report = validator.Validate(
            world.RepoRoot.CombineWithFilePath("tests/Smoke.csproj"),
            world.RepoRoot.CombineWithFilePath("tests/CompileSanity.csproj"),
            world.RepoRoot.Combine("artifacts/packages"));

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(3);
    }

    [Test]
    public void Constructor_Should_Throw_When_CakeContext_Null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PackageConsumerSmokePreconditionsValidator(cakeContext: null!));
    }
}
