using System.Text.Json;
using Build.Application.Packaging;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Harvesting.Models;
using Build.Domain.Packaging;
using Build.Domain.Packaging.Models;
using Build.Domain.Packaging.Results;
using Build.Infrastructure.DotNet;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Unit.Application.Packaging;

/// <summary>
/// Post-S1 (2026-04-17): <see cref="PackageTaskRunner"/> runs a 2-step per-family flow —
/// Pack(native) then Pack(managed), both with <c>$(Version)</c>, native additionally with
/// <c>$(NativePayloadSource)</c>. No 4-step orchestration, no <c>BuildProjectReferences=false</c>.
/// </summary>
public sealed class PackageTaskRunnerTests
{
    [Test]
    public async Task RunAsync_Should_Pack_Native_Then_Managed_For_Each_Selected_Family()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var family = manifest.PackageFamilies.Single(static packageFamily => packageFamily.Name == "sdl2-core");

        // Post-H1: pack gate validates runtimes/ + licenses/_consolidated/ specifically.
        // The happy-path fixture must seed the consolidated layout (not library-flat) or
        // the gate would reject — which is exactly the class of regression the tightened
        // gate exists to catch.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithTextFile("artifacts/harvest_output/SDL2/harvest-manifest.json", JsonSerializer.Serialize(CreateHarvestManifest("SDL2")))
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/win-x64/native/SDL2.dll", "payload")
            .WithTextFile("artifacts/harvest_output/SDL2/licenses/_consolidated/sdl2/copyright", "license")
            .BuildContextWithHandles();

        var processRunner = Substitute.For<IProcessRunner>();
        using var process = new FakeProcess();
        process.SetExitCode(0);
        process.SetStandardOutput(["0123456789abcdef0123456789abcdef01234567"]);
        process.SetStandardError([]);
        processRunner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>()).Returns(process);
        repo.CakeContext.ProcessRunner.Returns(processRunner);

        var packageFamilySelector = Substitute.For<IPackageFamilySelector>();
        packageFamilySelector.Select(Arg.Any<IReadOnlyList<string>>())
            .Returns(new PackageFamilySelection([family]));


        var dotNetPackInvoker = Substitute.For<IDotNetPackInvoker>();
        dotNetPackInvoker.Pack(
            Arg.Any<FilePath>(),
            Arg.Any<DotNetPackInvocation>(),
            Arg.Any<bool>(),
            Arg.Any<bool>()).Returns(DotNetPackResult.ToSuccess());

        var nativePackageMetadataGenerator = Substitute.For<INativePackageMetadataGenerator>();
        nativePackageMetadataGenerator.GenerateAsync(
                Arg.Any<PackageFamilyConfig>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var readmeMappingTableGenerator = Substitute.For<IReadmeMappingTableGenerator>();
        readmeMappingTableGenerator.UpdateAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var projectMetadataReader = Substitute.For<IProjectMetadataReader>();
        ProjectMetadataResult metadataResult = new ProjectMetadata(
            ["net9.0", "net8.0", "netstandard2.0", "net462"],
            "Authors",
            "LICENSE",
            "icon.png");
        projectMetadataReader.ReadAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(metadataResult);

        var packageOutputValidator = Substitute.For<IPackageOutputValidator>();
        packageOutputValidator
            .ValidateAsync(
                Arg.Any<PackageFamilyConfig>(),
                Arg.Any<PackageArtifacts>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<ProjectMetadata>(),
                Arg.Any<ManifestConfig>(),
                Arg.Any<FilePath>())
            .Returns(PackageValidationResult.Pass(new PackageValidation([])));

        var runner = new PackageTaskRunner(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            new DotNetBuildConfiguration("Release"),
            new PackageBuildConfiguration(new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase) { ["sdl2-core"] = NuGetVersion.Parse("1.2.3") }),
            packageFamilySelector,
            dotNetPackInvoker,
            nativePackageMetadataGenerator,
            readmeMappingTableGenerator,
            projectMetadataReader,
            packageOutputValidator);

        await runner.RunAsync();

        // Step 1: Pack native — standard `dotnet pack`, native csproj receives $(NativePayloadSource).
        dotNetPackInvoker.Received(1).Pack(
            Arg.Is<FilePath>(path => string.Equals(path.FullPath, repo.ResolveFile(family.NativeProject!).FullPath, StringComparison.OrdinalIgnoreCase)),
            Arg.Is<DotNetPackInvocation>(invocation =>
                string.Equals(invocation.Version, "1.2.3", StringComparison.Ordinal) &&
                invocation.NativePayloadSource != null &&
                string.Equals(invocation.NativePayloadSource.FullPath, repo.Paths.HarvestOutput.Combine(family.LibraryRef).FullPath, StringComparison.OrdinalIgnoreCase)),
            noRestore: false,
            noBuild: false);

        // Step 2: Pack managed — standard `dotnet pack`, no $(NativePayloadSource) threading.
        // Managed's ProjectReference to the native emits as a standard minimum-range dependency
        // (post-S1 SkiaSharp-style; no exact-pin CPM plumbing).
        dotNetPackInvoker.Received(1).Pack(
            Arg.Is<FilePath>(path => string.Equals(path.FullPath, repo.ResolveFile(family.ManagedProject!).FullPath, StringComparison.OrdinalIgnoreCase)),
            Arg.Is<DotNetPackInvocation>(invocation =>
                string.Equals(invocation.Version, "1.2.3", StringComparison.Ordinal) &&
                invocation.NativePayloadSource == null),
            noRestore: false,
            noBuild: false);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Harvest_Manifest_Lacks_Consolidation_Receipt()
    {
        // H1 gate: harvest-manifest.json without a Consolidation section is either legacy
        // pre-H1 state OR a manifest the operator hand-crafted without running the new
        // ConsolidateHarvestTask. Either way, Pack must refuse — the native csproj packs
        // from licenses/_consolidated/ which may be empty or absent.
        var manifest = ManifestFixture.CreateTestManifestConfig();

        var legacyManifest = CreateHarvestManifest("SDL2") with { Consolidation = null };

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithTextFile("artifacts/harvest_output/SDL2/harvest-manifest.json", JsonSerializer.Serialize(legacyManifest))
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/win-x64/native/SDL2.dll", "payload")
            .WithTextFile("artifacts/harvest_output/SDL2/licenses/win-x64/sdl2/copyright", "license")
            .BuildContextWithHandles();

        var runner = BuildRunnerWithMinimalMocks(repo, manifest);

        var thrown = await Assert.That(() => runner.RunAsync()).Throws<Cake.Core.CakeException>();
        await Assert.That(thrown!.Message).Contains("lacks a consolidation receipt");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Consolidation_Reports_Zero_License_Entries()
    {
        // H1 gate: a Consolidation section with LicensesConsolidated=false (or
        // LicenseEntriesCount=0) means no successful RID contributed license evidence —
        // Pack would ship a nupkg with no attribution. Fail with a clear compliance message.
        var manifest = ManifestFixture.CreateTestManifestConfig();

        var emptyConsolidationManifest = CreateHarvestManifest("SDL2") with
        {
            Consolidation = new ConsolidationState
            {
                LicensesConsolidated = false,
                LicenseEntriesCount = 0,
                DivergentLicenses = [],
            },
        };

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithTextFile("artifacts/harvest_output/SDL2/harvest-manifest.json", JsonSerializer.Serialize(emptyConsolidationManifest))
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/win-x64/native/SDL2.dll", "payload")
            .WithTextFile("artifacts/harvest_output/SDL2/licenses/win-x64/sdl2/copyright", "license")
            .BuildContextWithHandles();

        var runner = BuildRunnerWithMinimalMocks(repo, manifest);

        var thrown = await Assert.That(() => runner.RunAsync()).Throws<Cake.Core.CakeException>();
        await Assert.That(thrown!.Message).Contains("zero license entries");
    }

    private static PackageTaskRunner BuildRunnerWithMinimalMocks(FakeRepoHandles repo, ManifestConfig manifest)
    {
        var family = manifest.PackageFamilies.Single(static packageFamily => packageFamily.Name == "sdl2-core");

        var processRunner = Substitute.For<IProcessRunner>();
        using var process = new FakeProcess();
        process.SetExitCode(0);
        process.SetStandardOutput(["0123456789abcdef0123456789abcdef01234567"]);
        process.SetStandardError([]);
        processRunner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>()).Returns(process);
        repo.CakeContext.ProcessRunner.Returns(processRunner);

        var packageFamilySelector = Substitute.For<IPackageFamilySelector>();
        packageFamilySelector.Select(Arg.Any<IReadOnlyList<string>>())
            .Returns(new PackageFamilySelection([family]));


        var dotNetPackInvoker = Substitute.For<IDotNetPackInvoker>();
        dotNetPackInvoker.Pack(Arg.Any<FilePath>(), Arg.Any<DotNetPackInvocation>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(DotNetPackResult.ToSuccess());

        var nativePackageMetadataGenerator = Substitute.For<INativePackageMetadataGenerator>();
        nativePackageMetadataGenerator.GenerateAsync(
                Arg.Any<PackageFamilyConfig>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var readmeMappingTableGenerator = Substitute.For<IReadmeMappingTableGenerator>();
        readmeMappingTableGenerator.UpdateAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var projectMetadataReader = Substitute.For<IProjectMetadataReader>();
        projectMetadataReader.ReadAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(new ProjectMetadata(["net9.0"], "Authors", "LICENSE", "icon.png"));

        var packageOutputValidator = Substitute.For<IPackageOutputValidator>();
        packageOutputValidator.ValidateAsync(
                Arg.Any<PackageFamilyConfig>(),
                Arg.Any<PackageArtifacts>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<ProjectMetadata>(),
                Arg.Any<ManifestConfig>(),
                Arg.Any<FilePath>())
            .Returns(PackageValidationResult.Pass(new PackageValidation([])));

        return new PackageTaskRunner(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            new DotNetBuildConfiguration("Release"),
            new PackageBuildConfiguration(new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase) { ["sdl2-core"] = NuGetVersion.Parse("1.2.3") }),
            packageFamilySelector,
            dotNetPackInvoker,
            nativePackageMetadataGenerator,
            readmeMappingTableGenerator,
            projectMetadataReader,
            packageOutputValidator);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Consolidated_License_Tree_Missing_Even_If_Receipt_Valid()
    {
        // H1 tightened gate: the native csproj packs only from licenses/_consolidated/**.
        // A manifest with a seemingly-valid Consolidation section but an empty or absent
        // _consolidated/ subtree on disk would silently produce an empty-license nupkg.
        // The gate must refuse the pre-pack rather than relying on post-pack G51 to catch it.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validManifest = CreateHarvestManifest("SDL2");

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithTextFile("artifacts/harvest_output/SDL2/harvest-manifest.json", JsonSerializer.Serialize(validManifest))
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/win-x64/native/SDL2.dll", "payload")
            // Per-RID license present (pre-H1 shape would have accepted this), but no
            // licenses/_consolidated/ subtree at all — the tightened gate must still reject.
            .WithTextFile("artifacts/harvest_output/SDL2/licenses/win-x64/sdl2/copyright", "license")
            .BuildContextWithHandles();

        var runner = BuildRunnerWithMinimalMocks(repo, manifest);

        var thrown = await Assert.That(() => runner.RunAsync()).Throws<Cake.Core.CakeException>();
        await Assert.That(thrown!.Message).Contains("licenses/_consolidated");
    }

    private static HarvestManifest CreateHarvestManifest(string libraryName)
    {
        return new HarvestManifest
        {
            LibraryName = libraryName,
            GeneratedTimestamp = DateTimeOffset.UtcNow,
            Rids =
            [
                new RidHarvestStatus
                {
                    LibraryName = libraryName,
                    Rid = "win-x64",
                    Triplet = "x64-windows-hybrid",
                    Success = true,
                    Timestamp = DateTimeOffset.UtcNow,
                    Statistics = new HarvestStatistics
                    {
                        PrimaryFilesCount = 1,
                        RuntimeFilesCount = 1,
                        LicenseFilesCount = 1,
                        DeployedPackagesCount = 1,
                        FilteredPackagesCount = 0,
                        DeploymentStrategy = "hybrid-static",
                    },
                },
            ],
            Summary = new HarvestSummary
            {
                TotalRids = 1,
                SuccessfulRids = 1,
                FailedRids = 0,
                SuccessRate = 1,
            },
            // H1: post-Consolidate receipt — harvest manifest carries the license
            // consolidation state. Gate in PackageTaskRunner rejects a null Consolidation.
            Consolidation = new ConsolidationState
            {
                LicensesConsolidated = true,
                LicenseEntriesCount = 1,
                DivergentLicenses = [],
            },
        };
    }
}
