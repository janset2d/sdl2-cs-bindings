using System.Collections.Immutable;
using Build.Targets.Package.Models;
using Build.Host.Paths;
using Build.Data.ProjectMetadata;
using Build.Data.Manifest.Models;
using Build.Results;
using Build.Targets.Package.Reporting;
using Build.Targets.Package.Services;
using Build.Tests.Fixtures;
using Build.Validation.Packaging;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Targets.Package.Services;

public sealed class PackageFamilyPackerTests
{
    [Test]
    public async Task PackAsync_Should_Throw_When_ManagedProject_Path_Empty()
    {
        var (packer, _) = BuildPacker();
        var family = TestFamily(name: "sdl2-core", managedProject: null, nativeProject: "src/native/SDL2.Core.Native/SDL2.Core.Native.csproj");

        var ex = await Assert.ThrowsAsync<CakeException>(async () =>
            await packer.PackAsync(ManifestFixture.CreateTestManifestConfig(), family, "2.32.0", "abc123sha", "Release", CancellationToken.None));

        await Assert.That(ex!.Message).Contains("missing manifest field 'managed_project'");
    }

    [Test]
    public async Task PackAsync_Should_Throw_When_NativeProject_Path_Empty()
    {
        var (packer, _) = BuildPacker();
        var family = TestFamily(name: "sdl2-core", managedProject: "src/SDL2.Core/SDL2.Core.csproj", nativeProject: null);

        var ex = await Assert.ThrowsAsync<CakeException>(async () =>
            await packer.PackAsync(ManifestFixture.CreateTestManifestConfig(), family, "2.32.0", "abc123sha", "Release", CancellationToken.None));

        await Assert.That(ex!.Message).Contains("missing manifest field 'native_project'");
    }

    [Test]
    public async Task PackAsync_Should_Throw_With_Aggregated_Error_Count_When_PostPack_Validation_Fails()
    {
        var failingReport = new ValidationReport(
        [
            new ValidationCheck("Within-family minimum range", ValidationSeverity.Error, "managed dep version mismatch", Code: "G21"),
            new ValidationCheck("TFM agreement", ValidationSeverity.Error, "net8 group disagrees with net10", Code: "G22"),
            new ValidationCheck("Repository commit", ValidationSeverity.Error, "expected sha did not match", Code: "G26"),
        ]);
        var (packer, log) = BuildPacker(outputValidatorReport: failingReport);
        var family = TestFamily(name: "sdl2-core", managedProject: "src/SDL2.Core/SDL2.Core.csproj", nativeProject: "src/native/SDL2.Core.Native/SDL2.Core.Native.csproj");

        var ex = await Assert.ThrowsAsync<CakeException>(async () =>
            await packer.PackAsync(ManifestFixture.CreateTestManifestConfig(), family, "2.32.0", "abc123sha", "Release", CancellationToken.None));

        await Assert.That(ex!.Message).Contains("post-pack validation failed with 3 error(s)");
        // PackageReporter formats errors as "  - [{Code}] {Message}" via ICakeLog.Error.
        await Assert.That(log.HasMessage(LogLevel.Error, "[G21]")).IsTrue();
        await Assert.That(log.HasMessage(LogLevel.Error, "[G22]")).IsTrue();
        await Assert.That(log.HasMessage(LogLevel.Error, "[G26]")).IsTrue();
    }

    private static (PackageFamilyPacker Packer, TestLogV2 Log) BuildPacker(ValidationReport? outputValidatorReport = null)
    {
        var log = new TestLogV2();

        var pathService = Substitute.For<IPathService>();
        pathService.RepoRoot.Returns(new DirectoryPath("C:/repo"));
        pathService.PackagesOutput.Returns(new DirectoryPath("C:/repo/artifacts/packages"));
        pathService.GetHarvestLibraryDir(Arg.Any<string>())
            .Returns(call => new DirectoryPath($"C:/repo/artifacts/harvest_output/{(string)call[0]}"));
        pathService.GetReadmeFile().Returns(new FilePath("C:/repo/README.md"));

        var dotNetPackInvoker = Substitute.For<IDotNetPackInvoker>();
        dotNetPackInvoker.Pack(Arg.Any<FilePath>(), Arg.Any<DotNetPackInvocation>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(Result<Build.Results.Unit, DotNetPackError>.Success(Build.Results.Unit.Value));

        var nativeMetadataGenerator = Substitute.For<INativePackageMetadataGenerator>();
        nativeMetadataGenerator.GenerateAsync(Arg.Any<ManifestConfig>(), Arg.Any<PackageFamilyConfig>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var projectMetadataReader = Substitute.For<IProjectMetadataReader>();
        projectMetadataReader.Read(Arg.Any<FilePath>())
            .Returns(Result<EvaluatedProjectMetadata, ProjectMetadataError>.Success(
                new EvaluatedProjectMetadata(["net10.0"], "Authors", "LICENSE", "icon.png")));

        var packageOutputValidator = Substitute.For<IPackageOutputValidator>();
        packageOutputValidator.ValidateAsync(
            Arg.Any<PackageFamilyConfig>(),
            Arg.Any<PackageArtifacts>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<EvaluatedProjectMetadata>(),
            Arg.Any<ManifestConfig>(),
            Arg.Any<FilePath>())
            .Returns(outputValidatorReport ?? ValidationReport.Empty);

        var harvestReadinessValidator = Substitute.For<IHarvestReadinessValidator>();
        harvestReadinessValidator.EnsureReadyAsync(Arg.Any<PackageFamilyConfig>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // DependencyRangeNormalizer needs an ICakeContext + ICakeLog. Use a minimal world.
        var world = FakeCakeWorldV2.CreateWindows();
        var normalizer = new DependencyRangeNormalizer(world.CakeContext, world.Log);

        var reporter = new PackageReporter(log);

        var packer = new PackageFamilyPacker(
            pathService,
            dotNetPackInvoker,
            nativeMetadataGenerator,
            projectMetadataReader,
            packageOutputValidator,
            harvestReadinessValidator,
            normalizer,
            reporter);

        return (packer, log);
    }

    private static PackageFamilyConfig TestFamily(string name, string? managedProject, string? nativeProject) => new()
    {
        Name = name,
        TagPrefix = $"{name}/",
        LibraryRef = "sdl2",
        ManagedProject = managedProject,
        NativeProject = nativeProject,
        DependsOn = ImmutableList<string>.Empty,
        ChangePaths = ImmutableList<string>.Empty,
    };
}
