using Build.Data.Harvest;
using Build.Host.Cake;
using Build.Data;
using Build.Data.Manifest.Models;
using Build.Results;
using Build.Targets.Harvest;
using Build.Targets.Harvest.Models;
using Build.Targets.Harvest.Services;
using Build.Tests.Fixtures;
using Build.Validation;
using Build.Validation.Harvesting;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Build.Tests.Scenarios.Harvest;

/// <summary>
/// In-process behavior coverage for <c>HarvestTask</c>: exercises real task orchestration
/// against the fake Cake world. Focuses on the orchestration seams introduced by the
/// migration: task-entry validation, cohort precondition gate, and the failure-path
/// promise that <c>HarvestStatusRepository.WriteErrorAsync</c> persists a rid-status JSON
/// before <c>CakeException</c> propagates. Per-collaborator success/failure paths
/// (walker, planner, deployer) live in <c>Unit/Targets/Harvest/Services/</c>.
/// </summary>
public sealed class HarvestTaskScenarioTests
{
    private const string LibraryName = "sdl2-core";
    private const string Rid = "win-x64";

    [Test]
    public async Task RunAsync_Should_Throw_When_Rid_Is_Missing()
    {
        var world = FakeCakeWorld.CreateWindows().WithRid("");
        var manifest = ManifestFixture.CreateTestManifestConfig();

        var result = await CreateHost(world, manifest).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Harvest requires --rid", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Vcpkg_Triplet_Dir_Is_Missing()
    {
        // No vcpkg_installed dir seeded -> HarvestPreconditionsValidator fails before any
        // walker/planner/deployer work. Verifies cohort precondition gates the task body.
        var world = FakeCakeWorld.CreateWindows().WithRid(Rid);
        var manifest = ManifestFixture.CreateTestManifestConfig();

        var result = await CreateHost(world, manifest).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("vcpkg triplet directory", StringComparison.Ordinal);
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "vcpkg triplet directory")).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Persist_Error_Rid_Status_When_Closure_Walker_Fails()
    {
        // Walker returns a ClosureNotFound error -> HarvestTask must:
        //   1. report the failure via the reporter (red rule + error log)
        //   2. write a failure-flagged rid-status JSON via HarvestStatusRepository
        //   3. surface a CakeException to the caller
        // This scenario asserts on (2) end-to-end + (3); (1) is covered in HarvestReporterTests.
        var manifest = CreateSingleLibraryManifest();
        var world = SeedVcpkgInstall();

        var walkerStub = Substitute.For<IBinaryClosureWalker>();
        walkerStub.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns(Result<BinaryClosure, ClosureError>.Failure(new ClosureNotFound("vcpkg cache miss for sdl2")));

        var result = await CreateHost(world, manifest, walkerOverride: walkerStub).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Binary closure failed", StringComparison.Ordinal);
        var statusFileRelative = $"artifacts/harvest_output/{LibraryName}/rid-status/{Rid}.json";
        await Assert.That(world.FileExists(statusFileRelative)).IsTrue();
        var status = await world.CakeContext.ToJsonAsync<RidHarvestStatus>(world.RepoRoot.CombineWithFilePath(statusFileRelative));
        await Assert.That(status.Success).IsFalse();
        await Assert.That(status.ErrorMessage).IsNotNull();
        await Assert.That(status.ErrorMessage!).Contains("vcpkg cache miss for sdl2", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Persist_Success_Rid_Status_When_Library_Harvest_Completes()
    {
        // Happy path: walker + planner + deployer all return success; G1 invariant satisfied.
        // Asserts the end-to-end success contract — rid-status JSON written with success=true,
        // statistics populated, and the completion rule rendered to the console.
        var manifest = CreateSingleLibraryManifest();
        var world = SeedVcpkgInstall();

        var stats = CreateNonEmptyStatistics();
        var walkerStub = StubWalkerSuccess();
        var leakStub = StubLeakValidatorClean();
        var plannerStub = StubPlannerSuccess(stats);
        var deployerStub = StubDeployerSuccess(stats);

        var result = await CreateHost(world, manifest, walkerStub, leakStub, plannerStub, deployerStub).RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Exception).IsNull();
        var statusFileRelative = $"artifacts/harvest_output/{LibraryName}/rid-status/{Rid}.json";
        await Assert.That(world.FileExists(statusFileRelative)).IsTrue();
        var status = await world.CakeContext.ToJsonAsync<RidHarvestStatus>(world.RepoRoot.CombineWithFilePath(statusFileRelative));
        await Assert.That(status.Success).IsTrue();
        await Assert.That(status.Statistics).IsNotNull();
        await Assert.That(status.Statistics!.PrimaryFilesCount).IsEqualTo(1);
        await Assert.That(world.AnsiConsole.Output).Contains("Harvest completed successfully", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Not_Persist_Rid_Status_When_Cancellation_Requested()
    {
        // OperationCanceledException must NOT write an error rid-status — cancellation is not
        // a harvest failure. Reporter renders a cancel rule; the task re-throws so Cake host
        // surfaces the cancellation.
        var manifest = CreateSingleLibraryManifest();
        var world = SeedVcpkgInstall();

        var walkerStub = Substitute.For<IBinaryClosureWalker>();
        walkerStub.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<BinaryClosure, ClosureError>>>(_ => throw new OperationCanceledException());

        var result = await CreateHost(world, manifest, walkerOverride: walkerStub).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception).IsTypeOf<OperationCanceledException>();
        var statusFileRelative = $"artifacts/harvest_output/{LibraryName}/rid-status/{Rid}.json";
        await Assert.That(world.FileExists(statusFileRelative)).IsFalse();
        await Assert.That(world.AnsiConsole.Output).Contains("Canceled Harvest", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Persist_Error_Rid_Status_When_Operational_Exception_Wrapped()
    {
        // IOException out of the walker is operational (matches IsOperationalHarvestException).
        // The task wraps it into a CakeException at the boundary and persists an error rid-status
        // before the throw escapes — the catch-block at the bottom of ProcessLibraryAsync.
        var manifest = CreateSingleLibraryManifest();
        var world = SeedVcpkgInstall();

        var walkerStub = Substitute.For<IBinaryClosureWalker>();
        walkerStub.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<BinaryClosure, ClosureError>>>(_ => throw new IOException("disk full"));

        var result = await CreateHost(world, manifest, walkerOverride: walkerStub).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Harvest failed", StringComparison.Ordinal);
        var statusFileRelative = $"artifacts/harvest_output/{LibraryName}/rid-status/{Rid}.json";
        await Assert.That(world.FileExists(statusFileRelative)).IsTrue();
        var status = await world.CakeContext.ToJsonAsync<RidHarvestStatus>(world.RepoRoot.CombineWithFilePath(statusFileRelative));
        await Assert.That(status.Success).IsFalse();
        await Assert.That(status.ErrorMessage).IsNotNull();
        await Assert.That(status.ErrorMessage!).Contains("disk full", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Persist_Error_Rid_Status_When_Leak_Validation_Fails()
    {
        // Walker succeeds; leak validator returns an invalid report. HarvestTask invokes
        // ReportLeakReport BEFORE failing so the operator sees the leak details, then persists
        // an error rid-status with the violation count.
        var manifest = CreateSingleLibraryManifest();
        var world = SeedVcpkgInstall();

        var walkerStub = StubWalkerSuccess();
        var leakStub = Substitute.For<IHybridStaticLeakValidator>();
        leakStub.Validate(Arg.Any<BinaryClosure>(), Arg.Any<LibraryManifest>(), Arg.Any<string>(), Arg.Any<ValidationMode>())
            .Returns(new ValidationReport([
                new ValidationCheck("LeakCheck", ValidationSeverity.Error, "leak: zlib1.dll leaks transitive dep"),
            ]));

        var result = await CreateHost(world, manifest, walkerStub, leakStub).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Hybrid-static leak validation failed", StringComparison.Ordinal);
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "zlib1.dll leaks")).IsTrue();
        var statusFileRelative = $"artifacts/harvest_output/{LibraryName}/rid-status/{Rid}.json";
        await Assert.That(world.FileExists(statusFileRelative)).IsTrue();
        var status = await world.CakeContext.ToJsonAsync<RidHarvestStatus>(world.RepoRoot.CombineWithFilePath(statusFileRelative));
        await Assert.That(status.Success).IsFalse();
        await Assert.That(status.ErrorMessage).IsNotNull();
        await Assert.That(status.ErrorMessage!).Contains("violation(s) detected", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Persist_Error_Rid_Status_When_Zero_Primary_Files_Deployed()
    {
        // [G1] post-harvest invariant: deployer reported success but produced zero primary
        // binaries. The task fails the library and persists an error rid-status so downstream
        // consumers don't ingest a success record with an empty payload.
        var manifest = CreateSingleLibraryManifest();
        var world = SeedVcpkgInstall();

        var emptyStats = CreateEmptyStatistics();
        var walkerStub = StubWalkerSuccess();
        var leakStub = StubLeakValidatorClean();
        var plannerStub = StubPlannerSuccess(emptyStats);
        var deployerStub = StubDeployerSuccess(emptyStats);

        var result = await CreateHost(world, manifest, walkerStub, leakStub, plannerStub, deployerStub).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Harvest failed", StringComparison.Ordinal);
        var statusFileRelative = $"artifacts/harvest_output/{LibraryName}/rid-status/{Rid}.json";
        await Assert.That(world.FileExists(statusFileRelative)).IsTrue();
        var status = await world.CakeContext.ToJsonAsync<RidHarvestStatus>(world.RepoRoot.CombineWithFilePath(statusFileRelative));
        await Assert.That(status.Success).IsFalse();
        await Assert.That(status.ErrorMessage).IsNotNull();
        await Assert.That(status.ErrorMessage!).Contains("zero primary binaries", StringComparison.Ordinal);
    }

    private static FakeCakeWorld SeedVcpkgInstall() =>
        FakeCakeWorld.CreateWindows()
            .WithRid(Rid)
            .WithTextFile("vcpkg_installed/x64-windows-hybrid/.placeholder", string.Empty);

    private static IBinaryClosureWalker StubWalkerSuccess()
    {
        var walker = Substitute.For<IBinaryClosureWalker>();
        var closure = new BinaryClosure(
            new HashSet<string>(StringComparer.Ordinal) { "C:/vcpkg_installed/x64-windows-hybrid/bin/SDL2.dll" },
            [new BinaryNode("C:/vcpkg_installed/x64-windows-hybrid/bin/SDL2.dll", "sdl2", "sdl2")],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2" });
        walker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns(Result<BinaryClosure, ClosureError>.Success(closure));
        return walker;
    }

    private static IHybridStaticLeakValidator StubLeakValidatorClean()
    {
        var leak = Substitute.For<IHybridStaticLeakValidator>();
        leak.Validate(Arg.Any<BinaryClosure>(), Arg.Any<LibraryManifest>(), Arg.Any<string>(), Arg.Any<ValidationMode>())
            .Returns(ValidationReport.Empty);
        return leak;
    }

    private static IArtifactPlanner StubPlannerSuccess(DeploymentStatistics statistics)
    {
        var planner = Substitute.For<IArtifactPlanner>();
        var plan = new DeploymentPlan([], statistics);
        planner.CreatePlanAsync(Arg.Any<LibraryManifest>(), Arg.Any<BinaryClosure>(), Arg.Any<DirectoryPath>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<DeploymentPlan, ArtifactPlannerError>.Success(plan));
        return planner;
    }

    private static IArtifactDeployer StubDeployerSuccess(DeploymentStatistics statistics)
    {
        var deployer = Substitute.For<IArtifactDeployer>();
        deployer.DeployArtifactsAsync(Arg.Any<DeploymentPlan>(), Arg.Any<CancellationToken>())
            .Returns(Result<DeploymentStatistics, CopierError>.Success(statistics));
        return deployer;
    }

    private static DeploymentStatistics CreateNonEmptyStatistics() =>
        new(
            LibraryName,
            [new FileDeploymentInfo(new FilePath("SDL2.dll"), "sdl2", DeploymentLocation.FileSystem)],
            [],
            [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            DeploymentStrategy.DirectCopy);

    private static DeploymentStatistics CreateEmptyStatistics() =>
        new(
            LibraryName,
            [],
            [],
            [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            DeploymentStrategy.DirectCopy);

    private static TargetTestHost<HarvestTask> CreateHost(
        FakeCakeWorld world,
        ManifestConfig? manifest = null,
        IBinaryClosureWalker? walkerOverride = null,
        IHybridStaticLeakValidator? leakValidatorOverride = null,
        IArtifactPlanner? plannerOverride = null,
        IArtifactDeployer? deployerOverride = null)
    {
        var host = new TargetTestHost<HarvestTask>(world);
        if (manifest is not null)
        {
            host.WithManifest(manifest);
        }

        return host.WithServices(services =>
        {
            // Stub external scanner integration so scenarios never invoke real tools against
            // the fake filesystem. Vcpkg package metadata is hidden behind walker/planner
            // overrides in these task-level scenarios.
            services.AddSingleton(Substitute.For<IRuntimeScanner>());

            services.AddData();
            services.AddValidators();
            services.AddHarvest();

            // Last-wins overrides: replace AddHarvest()/AddValidators()-registered collaborators
            // with scenario-specific stubs. Used by failure-path scenarios that mock the
            // collaborator outcome directly (avoids re-deriving full vcpkg-install / scanner
            // fixture surfaces).
            if (walkerOverride is not null)
            {
                services.AddSingleton(walkerOverride);
            }
            if (leakValidatorOverride is not null)
            {
                services.AddSingleton(leakValidatorOverride);
            }
            if (plannerOverride is not null)
            {
                services.AddSingleton(plannerOverride);
            }
            if (deployerOverride is not null)
            {
                services.AddSingleton(deployerOverride);
            }
        });
    }

    private static ManifestConfig CreateSingleLibraryManifest()
    {
        var baseline = ManifestFixture.CreateTestManifestConfig();
        var library = ManifestFixture.CreateTestCoreLibrary() with { Name = LibraryName };
        return baseline with
        {
            LibraryManifests = [library],
        };
    }
}
