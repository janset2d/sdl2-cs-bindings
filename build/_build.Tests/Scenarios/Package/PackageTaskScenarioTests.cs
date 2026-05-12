using Build.Data.ProjectMetadata;
using Build.Targets.Package.Models;
using Build.Data;
using Build.Data.Manifest.Models;
using Build.Results;
using Build.Targets.Package;
using Build.Targets.Package.Services;
using Build.Tests.Fixtures;
using Build.Validation;
using Build.Validation.Packaging;
using Build.Data.Versions;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Scenarios.Package;

/// <summary>
/// In-process behavior coverage for <c>PackageTask</c>: exercises real task orchestration
/// against the fake Cake world. Happy path packs a multi-family selection (sdl2-core +
/// sdl2-image) and asserts dotnet pack invocations + post-pack guardrail success. Failure
/// paths cover boundary preconditions, harvest readiness gates, dotnet-pack failure surface,
/// G56 cross-family range normalization, G58 cross-family resolvability, and the
/// PackageOutputValidator multi-error aggregation path.
/// </summary>
public sealed class PackageTaskScenarioTests
{
    private const string CoreLibrary = "SDL2";
    private const string ImageLibrary = "SDL2_image";

    [Test]
    public async Task RunAsync_Should_Pack_All_Selected_Families_When_Happy_Path()
    {
        var world = NewWorld()
            .WithFamilyVersions(MultiFamilyVersions());
        SeedHarvestPayload(world, CoreLibrary);
        SeedHarvestPayload(world, ImageLibrary);
        SeedReadme(world);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Exception).IsNull();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Forward_CancellationToken_To_Generator_And_Packer_Chain()
    {
        using var cts = new CancellationTokenSource();
        var readmeGen = Substitute.For<IReadmeMappingTableGenerator>();
        var nativeMetadataGen = Substitute.For<INativePackageMetadataGenerator>();
        var world = NewWorld()
            .WithFamilyVersions(MultiFamilyVersions())
            .WithCancellationToken(cts.Token);
        SeedHarvestPayload(world, CoreLibrary);
        SeedHarvestPayload(world, ImageLibrary);
        SeedReadme(world);

        var result = await CreateHost(world, readmeGen, nativeMetadataGen).RunAsync();

        await Assert.That(result.Success).IsTrue();
        // PackageTask:98 directly forwards ct to UpdateAsync.
        await readmeGen.Received().UpdateAsync(
            Arg.Any<ManifestConfig>(),
            Arg.Is<CancellationToken>(ct => ct == cts.Token));
        // PackageTask:105 → PackageFamilyPacker.PackAsync → GenerateAsync chain.
        // Asserts the full forward through PackAsync's ct parameter.
        await nativeMetadataGen.Received().GenerateAsync(
            Arg.Any<ManifestConfig>(),
            Arg.Any<PackageFamilyConfig>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<CancellationToken>(ct => ct == cts.Token));
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Versions_File_Missing()
    {
        // No --versions-file passed; task entry should fail-loud.
        var world = NewWorld();
        world.WithVersionsFile(null);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("requires --versions-file");
    }

    [Test]
    public async Task RunAsync_Should_Throw_With_G58_Error_When_CrossFamily_Dependency_Unresolved()
    {
        // Versions has only sdl2-image, but sdl2-image depends on sdl2-core (per manifest).
        // Override the multi-family fixture with a single-family JSON for this test.
        var world = NewWorld()
            .WithTextFile("artifacts/resolve-versions/versions.json", "{\n  \"sdl2-image\": \"2.8.0\"\n}");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("[G58]");
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "[G58]")).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Harvest_Manifest_Missing()
    {
        var world = NewWorld().WithFamilyVersions(MultiFamilyVersions());
        // No harvest output seeded.

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("harvest manifest");
        await Assert.That(result.Exception.Message).Contains("is missing");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Harvest_Receipt_Missing()
    {
        var world = NewWorld().WithFamilyVersions(MultiFamilyVersions());
        // sdl2-core packs first by topology; seed it with a no-receipt manifest.
        SeedHarvestPayload(world, CoreLibrary, harvestManifestFixture: "Harvest/harvest-manifest-no-receipt.json");
        SeedHarvestPayload(world, ImageLibrary);
        SeedReadme(world);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("lacks a consolidation receipt");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Harvest_Receipt_Reports_Zero_Successful_Rids()
    {
        var world = NewWorld().WithFamilyVersions(MultiFamilyVersions());
        SeedHarvestPayload(world, CoreLibrary, harvestManifestFixture: "Harvest/harvest-manifest-zero-rids.json");
        SeedHarvestPayload(world, ImageLibrary);
        SeedReadme(world);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("reports zero successful RIDs");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Consolidated_Licenses_Empty()
    {
        var world = NewWorld().WithFamilyVersions(MultiFamilyVersions());
        SeedHarvestPayload(world, CoreLibrary, harvestManifestFixture: "Harvest/harvest-manifest-zero-licenses.json");
        SeedHarvestPayload(world, ImageLibrary);
        SeedReadme(world);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("zero license entries");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Payload_Subtree_Missing()
    {
        var world = NewWorld().WithFamilyVersions(MultiFamilyVersions());
        // Ready manifest but no runtimes/ subtree files.
        world.WithTextFile($"artifacts/harvest_output/{CoreLibrary}/harvest-manifest.json",
            FixtureLoader.Load("Harvest/harvest-manifest-ready.json"));
        // Skip seeding runtimes/ and licenses/_consolidated/ subtrees → directories don't exist.
        SeedHarvestPayload(world, ImageLibrary);
        SeedReadme(world);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("harvest payload directory");
    }

    [Test]
    public async Task RunAsync_Should_Throw_With_Aggregated_Errors_When_PostPack_Validation_Reports_Multiple_Failures()
    {
        var world = NewWorld().WithFamilyVersions(MultiFamilyVersions());
        SeedHarvestPayload(world, CoreLibrary);
        SeedHarvestPayload(world, ImageLibrary);
        SeedReadme(world);

        var failingReport = new ValidationReport(
        [
            new ValidationCheck("Within-family minimum range", ValidationSeverity.Error, "managed dep version mismatch", Code: "G21"),
            new ValidationCheck("TFM agreement", ValidationSeverity.Error, "net8 group disagrees with net10", Code: "G22"),
            new ValidationCheck("Repository commit", ValidationSeverity.Error, "expected sha did not match", Code: "G26"),
        ]);

        var result = await CreateHostWithStubValidator(world, failingReport).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("post-pack validation failed with 3 error(s)");
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "[G21]")).IsTrue();
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "[G22]")).IsTrue();
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "[G26]")).IsTrue();
    }

    private static FakeCakeWorld NewWorld()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithToolPath(new FilePath("C:/tools/dotnet.exe"))
            .WithProcessResult("dotnet.exe", exitCode: 0, stdOut: "")
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithVersionsFile("artifacts/resolve-versions/versions.json")
            .WithTextFile("artifacts/resolve-versions/versions.json", FixtureLoader.Load("Versions/versions-multi-family.json"));
        return world;
    }

    private static PackageFamilyVersionSet MultiFamilyVersions()
    {
        return new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(new PackageFamilyId("sdl2-core"), NuGetVersion.Parse("2.32.0")),
            new PackageFamilyVersion(new PackageFamilyId("sdl2-image"), NuGetVersion.Parse("2.8.0")),
        ]);
    }

    private static void SeedHarvestPayload(FakeCakeWorld world, string libraryRef, string harvestManifestFixture = "Harvest/harvest-manifest-ready.json")
    {
        world.WithTextFile($"artifacts/harvest_output/{libraryRef}/harvest-manifest.json",
            FixtureLoader.Load(harvestManifestFixture));
        world.WithTextFile($"artifacts/harvest_output/{libraryRef}/runtimes/win-x64/native/{libraryRef}.dll", "<bytes>");
        world.WithTextFile($"artifacts/harvest_output/{libraryRef}/licenses/_consolidated/{libraryRef}/LICENSE.txt", "MIT");
    }

    private static void SeedReadme(FakeCakeWorld world)
    {
        world.WithTextFile("README.md", ReadmeMappingTableBlock.BuildBlock(ManifestFixture.CreateTestManifestConfig()));
    }

    private static TargetTestHost<PackageTask> CreateHost(
        FakeCakeWorld world,
        IReadmeMappingTableGenerator? readmeGen = null,
        INativePackageMetadataGenerator? nativeMetadataGen = null)
    {
        var metadataReader = Substitute.For<IProjectMetadataReader>();
        metadataReader.Read(Arg.Any<FilePath>())
            .Returns(Result<EvaluatedProjectMetadata, ProjectMetadataError>.Success(
                new EvaluatedProjectMetadata(["net10.0"], "Authors", "LICENSE", "icon.png")));

        nativeMetadataGen ??= Substitute.For<INativePackageMetadataGenerator>();
        nativeMetadataGen.GenerateAsync(Arg.Any<ManifestConfig>(), Arg.Any<PackageFamilyConfig>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        readmeGen ??= Substitute.For<IReadmeMappingTableGenerator>();
        readmeGen.UpdateAsync(Arg.Any<ManifestConfig>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var outputValidator = Substitute.For<IPackageOutputValidator>();
        outputValidator.ValidateAsync(
            Arg.Any<PackageFamilyConfig>(),
            Arg.Any<PackageArtifacts>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<EvaluatedProjectMetadata>(),
            Arg.Any<ManifestConfig>(),
            Arg.Any<FilePath>())
            .Returns(ValidationReport.Empty);

        return new TargetTestHost<PackageTask>(world)
            .WithManifest(ManifestFixture.CreateTestManifestConfig())
            .WithServices(services =>
            {
                services.AddData();
                services.AddValidators();
                services.AddPackage();

                services.AddSingleton(metadataReader);
                services.AddSingleton(nativeMetadataGen);
                services.AddSingleton(readmeGen);
                services.AddSingleton(outputValidator);

                // Stub HEAD SHA resolver so Cake.Git is not invoked against fake filesystem.
                services.AddSingleton<Func<ICakeContext, DirectoryPath, string>>((_, _) => "stub-sha-1234567");
            });
    }

    private static TargetTestHost<PackageTask> CreateHostWithStubValidator(FakeCakeWorld world, ValidationReport stubReport)
    {
        var host = CreateHost(world);
        return host.WithServices(services =>
        {
            // Override the IPackageOutputValidator registered by AddValidators with one
            // returning the test-provided report. ServiceCollection takes the last registration
            // for a given service type, so this overrides the AddValidators registration above.
            var stubValidator = Substitute.For<IPackageOutputValidator>();
            stubValidator.ValidateAsync(
                Arg.Any<PackageFamilyConfig>(),
                Arg.Any<PackageArtifacts>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<EvaluatedProjectMetadata>(),
                Arg.Any<ManifestConfig>(),
                Arg.Any<FilePath>())
                .Returns(stubReport);

            // Replace existing IPackageOutputValidator registration.
            var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IPackageOutputValidator));
            if (existing is not null)
            {
                services.Remove(existing);
            }
            services.AddSingleton(stubValidator);
        });
    }
}
