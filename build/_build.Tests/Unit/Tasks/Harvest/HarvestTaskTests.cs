using System.Collections.Immutable;
using System.Text.Json;
using Build.Application.Harvesting;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Harvesting;
using Build.Domain.Harvesting.Models;
using Build.Domain.Harvesting.Results;
using Build.Domain.Runtime;
using Build.Domain.Strategy;
using Build.Domain.Strategy.Results;
using Build.Tests.Fixtures;
using Build.Tests.Fixtures.Seeders;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Tasks.Harvest;

public class HarvestTaskTests
{
    private const string CoreLibrary = "SDL2";
    private const string SatelliteLibrary = "SDL2_image";
    private const string WindowsTriplet = "x64-windows-hybrid";
    private const string WindowsRid = "win-x64";
    private const string LinuxRid = "linux-x64";

    [Test]
    public async Task RunAsync_Should_Generate_Success_Rid_Status_File_When_Harvest_Completes()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        SeedVcpkgTripletLayout(repo);

        var library = ManifestFixture.CreateTestCoreLibrary();
        var manifestConfig = CreateManifestConfig([library]);

        var task = CreateHarvestTask(
            manifestConfig,
            closure: ClosureWithSinglePrimary(CoreLibrary),
            plannerResult: PlannerResultWithSinglePrimary(CoreLibrary));

        await task.RunAsync(repo.BuildContext);

        var statusFilePath = RidStatusPath(CoreLibrary, WindowsRid);
        await Assert.That(repo.Exists(statusFilePath)).IsTrue();

        var status = await DeserializeStatusAsync(repo, statusFilePath);

        await Assert.That(status).IsNotNull();
        await Assert.That(status!.LibraryName).IsEqualTo(CoreLibrary);
        await Assert.That(status.Success).IsTrue();
        await Assert.That(status.ErrorMessage).IsNull();
        await Assert.That(status.Statistics).IsNotNull();
        await Assert.That(status.Statistics!.PrimaryFilesCount).IsEqualTo(1);
        await Assert.That(status.Statistics!.DeploymentStrategy).IsEqualTo("DirectCopy");
    }

    [Test]
    public async Task RunAsync_Should_Generate_Error_Rid_Status_File_When_Closure_Fails()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        SeedVcpkgTripletLayout(repo);

        var library = ManifestFixture.CreateTestCoreLibrary();
        var manifestConfig = CreateManifestConfig([library]);

        var mockWalker = Substitute.For<IBinaryClosureWalker>();
        mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns(new ClosureBuildError("dependency scan failed"));

        var mockPlanner = Substitute.For<IArtifactPlanner>();
        var mockDeployer = Substitute.For<IArtifactDeployer>();
        var mockValidator = CreatePassingValidator();
        var runtimeProfile = CreateWindowsRuntimeProfile();

        var task = new Build.Tasks.Harvest.HarvestTask(
            new HarvestTaskRunner(
                mockWalker,
                mockPlanner,
                mockDeployer,
                mockValidator,
                runtimeProfile,
                manifestConfig),
            new VcpkgConfiguration([], null));

        var thrown = false;
        try
        {
            await task.RunAsync(repo.BuildContext);
        }
        catch (CakeException ex)
        {
            thrown = true;
            await Assert.That(ex.Message).Contains("Binary closure failed");
        }

        await Assert.That(thrown).IsTrue();

        var statusFilePath = RidStatusPath(CoreLibrary, WindowsRid);
        await Assert.That(repo.Exists(statusFilePath)).IsTrue();

        var status = await DeserializeStatusAsync(repo, statusFilePath);

        await Assert.That(status).IsNotNull();
        await Assert.That(status!.Success).IsFalse();
        await Assert.That(status.ErrorMessage).IsEqualTo("Harvest failed for SDL2");
        await Assert.That(status.Statistics).IsNull();
    }

    [Test]
    public async Task RunAsync_Should_Generate_Error_Rid_Status_File_When_Operational_Exception_Occurs()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        SeedVcpkgTripletLayout(repo);

        var library = ManifestFixture.CreateTestCoreLibrary();
        var manifestConfig = CreateManifestConfig([library]);

        var mockWalker = Substitute.For<IBinaryClosureWalker>();
        mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ClosureResult>(new InvalidOperationException("planner inputs were inconsistent")));

        var mockPlanner = Substitute.For<IArtifactPlanner>();
        var mockDeployer = Substitute.For<IArtifactDeployer>();
        var mockValidator = CreatePassingValidator();
        var runtimeProfile = CreateWindowsRuntimeProfile();

        var task = new Build.Tasks.Harvest.HarvestTask(
            new HarvestTaskRunner(
                mockWalker,
                mockPlanner,
                mockDeployer,
                mockValidator,
                runtimeProfile,
                manifestConfig),
            new VcpkgConfiguration([], null));

        await Assert.That(() => task.RunAsync(repo.BuildContext)).Throws<InvalidOperationException>();

        var statusFilePath = RidStatusPath(CoreLibrary, WindowsRid);
        await Assert.That(repo.Exists(statusFilePath)).IsTrue();

        var status = await DeserializeStatusAsync(repo, statusFilePath);

        await Assert.That(status).IsNotNull();
        await Assert.That(status!.Success).IsFalse();
        await Assert.That(status.ErrorMessage).IsEqualTo("planner inputs were inconsistent");
    }

    [Test]
    public async Task RunAsync_Should_Propagate_Cancellation_Without_Generating_Error_Rid_Status_File()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        SeedVcpkgTripletLayout(repo);

        var library = ManifestFixture.CreateTestCoreLibrary();
        var manifestConfig = CreateManifestConfig([library]);

        var mockWalker = Substitute.For<IBinaryClosureWalker>();
        mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ClosureResult>(new OperationCanceledException("harvest canceled")));

        var mockPlanner = Substitute.For<IArtifactPlanner>();
        var mockDeployer = Substitute.For<IArtifactDeployer>();
        var mockValidator = CreatePassingValidator();
        var runtimeProfile = CreateWindowsRuntimeProfile();

        var task = new Build.Tasks.Harvest.HarvestTask(
            new HarvestTaskRunner(
                mockWalker,
                mockPlanner,
                mockDeployer,
                mockValidator,
                runtimeProfile,
                manifestConfig),
            new VcpkgConfiguration([], null));

        await Assert.That(() => task.RunAsync(repo.BuildContext)).Throws<OperationCanceledException>();

        var statusFilePath = RidStatusPath(CoreLibrary, WindowsRid);
        await Assert.That(repo.Exists(statusFilePath)).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Harvest_Only_Specified_Libraries_From_Vcpkg_Options()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithLibraries(SatelliteLibrary)
            .BuildContextWithHandles();
        SeedVcpkgTripletLayout(repo);

        var core = ManifestFixture.CreateTestCoreLibrary();
        var satellite = ManifestFixture.CreateTestSatelliteLibrary();
        var manifestConfig = CreateManifestConfig([core, satellite]);

        var task = CreateHarvestTask(
            manifestConfig,
            closure: ClosureWithSinglePrimary(SatelliteLibrary),
            plannerResult: PlannerResultWithSinglePrimary(SatelliteLibrary),
            vcpkgConfiguration: repo.BuildContext.Vcpkg);

        await task.RunAsync(repo.BuildContext);

        var selectedStatus = RidStatusPath(SatelliteLibrary, WindowsRid);
        var nonSelectedStatus = RidStatusPath(CoreLibrary, WindowsRid);

        await Assert.That(repo.Exists(selectedStatus)).IsTrue();
        await Assert.That(repo.Exists(nonSelectedStatus)).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Preserve_Other_Rids_While_Cleaning_Current_Rid_Output()
    {
        // Harvest cleans only the current RID's runtimes/, rid-status/, and licenses/
        // entries; sibling RIDs retain their evidence intact.
        // Consolidated licenses are invalidated because any RID re-run changes the union.
        var previousLinuxStatus = HarvestStatusSeeder.Success(CoreLibrary, LinuxRid, "x64-linux-hybrid");
        var previousWindowsStatus = HarvestStatusSeeder.Failure(CoreLibrary, WindowsRid, WindowsTriplet, "previous run failed");

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithTextFile($"artifacts/harvest_output/{CoreLibrary}/runtimes/{LinuxRid}/native/libSDL2.so", "keep")
            .WithTextFile($"artifacts/harvest_output/{CoreLibrary}/runtimes/{WindowsRid}/native/stale.dll", "stale")
            .WithTextFile($"artifacts/harvest_output/{CoreLibrary}/licenses/{LinuxRid}/zlib/copyright", "linux-license")
            .WithTextFile($"artifacts/harvest_output/{CoreLibrary}/licenses/{WindowsRid}/zlib/copyright", "stale-windows-license")
            .WithTextFile($"artifacts/harvest_output/{CoreLibrary}/licenses/_consolidated/zlib/copyright", "stale-consolidated")
            .Seed(previousLinuxStatus)
            .Seed(previousWindowsStatus)
            .BuildContextWithHandles();
        SeedVcpkgTripletLayout(repo);

        var library = ManifestFixture.CreateTestCoreLibrary();
        var manifestConfig = CreateManifestConfig([library]);

        var task = CreateHarvestTask(
            manifestConfig,
            closure: ClosureWithSinglePrimary(CoreLibrary),
            plannerResult: PlannerResultWithSinglePrimary(CoreLibrary));

        await task.RunAsync(repo.BuildContext);

        // Sibling RID's native + license payload preserved.
        await Assert.That(repo.Exists($"artifacts/harvest_output/{CoreLibrary}/runtimes/{LinuxRid}/native/libSDL2.so")).IsTrue();
        await Assert.That(repo.Exists($"artifacts/harvest_output/{CoreLibrary}/licenses/{LinuxRid}/zlib/copyright")).IsTrue();

        // Current RID's stale native + license evidence cleaned.
        await Assert.That(repo.Exists($"artifacts/harvest_output/{CoreLibrary}/runtimes/{WindowsRid}/native/stale.dll")).IsFalse();
        await Assert.That(repo.Exists($"artifacts/harvest_output/{CoreLibrary}/licenses/{WindowsRid}/zlib/copyright")).IsFalse();

        // Consolidated output invalidated — Consolidate regenerates it from surviving RIDs.
        await Assert.That(repo.Exists($"artifacts/harvest_output/{CoreLibrary}/licenses/_consolidated/zlib/copyright")).IsFalse();

        // Both RID status files remain (sibling from previous run, current from the fresh write).
        await Assert.That(repo.Exists(RidStatusPath(CoreLibrary, LinuxRid))).IsTrue();
        await Assert.That(repo.Exists(RidStatusPath(CoreLibrary, WindowsRid))).IsTrue();

        // Sibling RID status must remain valid real JSON after the current-RID cleanup pass.
        var preservedLinuxStatus = await DeserializeStatusAsync(repo, RidStatusPath(CoreLibrary, LinuxRid));
        await Assert.That(preservedLinuxStatus).IsNotNull();
        await Assert.That(preservedLinuxStatus!.Success).IsTrue();
        await Assert.That(preservedLinuxStatus.Rid).IsEqualTo(LinuxRid);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Dependency_Policy_Validation_Fails()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        SeedVcpkgTripletLayout(repo);

        var library = ManifestFixture.CreateTestSatelliteLibrary();
        var manifestConfig = CreateManifestConfig([library]);

        var mockWalker = Substitute.For<IBinaryClosureWalker>();
        mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns(ClosureWithSinglePrimary(SatelliteLibrary));

        var mockPlanner = Substitute.For<IArtifactPlanner>();
        var mockDeployer = Substitute.For<IArtifactDeployer>();

        var mockValidator = Substitute.For<IDependencyPolicyValidator>();
        mockValidator.Validate(Arg.Any<BinaryClosure>(), Arg.Any<LibraryManifest>())
            .Returns(ValidationResult.Fail(
            [new BinaryNode(new FilePath("C:/vcpkg/bin/zlib1.dll"), "zlib", "sdl2-image")],
            "dependency leak detected"));

        var runtimeProfile = CreateWindowsRuntimeProfile();
        var task = new Build.Tasks.Harvest.HarvestTask(
            new HarvestTaskRunner(
                mockWalker,
                mockPlanner,
                mockDeployer,
                mockValidator,
                runtimeProfile,
                manifestConfig),
            new VcpkgConfiguration([], null));

        await Assert.That(() => task.RunAsync(repo.BuildContext)).Throws<CakeException>();
    }

    [Test]
    public async Task RunAsync_Should_Clean_Orphan_Consolidate_Temp_Artifacts_On_Rid_Rerun()
    {
        // H1 completion: the staged-replace pattern in ConsolidateHarvestTask writes to
        // .tmp siblings before swapping into place. If Consolidate crashed mid-flight on
        // a previous run, those .tmp artifacts survive — the next Harvest invalidation
        // must sweep them so Consolidate can start fresh without conflicting with stale
        // tmp state.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithTextFile($"artifacts/harvest_output/{CoreLibrary}/licenses/_consolidated.tmp/zlib/copyright", "orphan-tmp-license")
            .WithTextFile($"artifacts/harvest_output/{CoreLibrary}/harvest-manifest.tmp.json", "{\"partial\":true}")
            .WithTextFile($"artifacts/harvest_output/{CoreLibrary}/harvest-summary.tmp.json", "{\"partial\":true}")
            .BuildContextWithHandles();
        SeedVcpkgTripletLayout(repo);

        var library = ManifestFixture.CreateTestCoreLibrary();
        var manifestConfig = CreateManifestConfig([library]);

        var task = CreateHarvestTask(
            manifestConfig,
            closure: ClosureWithSinglePrimary(CoreLibrary),
            plannerResult: PlannerResultWithSinglePrimary(CoreLibrary));

        await task.RunAsync(repo.BuildContext);

        await Assert.That(repo.Exists($"artifacts/harvest_output/{CoreLibrary}/licenses/_consolidated.tmp/zlib/copyright")).IsFalse();
        await Assert.That(repo.Exists($"artifacts/harvest_output/{CoreLibrary}/harvest-manifest.tmp.json")).IsFalse();
        await Assert.That(repo.Exists($"artifacts/harvest_output/{CoreLibrary}/harvest-summary.tmp.json")).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Invalidate_Cross_Rid_Consolidate_Receipts_On_Rid_Rerun()
    {
        // H1 invariant: HarvestTask deletes harvest-manifest.json + harvest-summary.json on
        // every RID run. PackageTaskRunner's gate fails when harvest-manifest is missing, so
        // this invalidation forces a Consolidate re-run before Pack can proceed. Without this,
        // a Harvest run against stale _consolidated/ would let Pack silently ship a nupkg
        // with no third-party license entries.
        const string manifestPath = "artifacts/harvest_output/SDL2/harvest-manifest.json";
        const string summaryPath = "artifacts/harvest_output/SDL2/harvest-summary.json";

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithTextFile(manifestPath, "{\"library_name\":\"SDL2\",\"stale\":true}")
            .WithTextFile(summaryPath, "{\"stale\":true}")
            .WithTextFile("artifacts/harvest_output/SDL2/licenses/_consolidated/zlib/copyright", "stale-consolidated")
            .BuildContextWithHandles();
        SeedVcpkgTripletLayout(repo);

        var library = ManifestFixture.CreateTestCoreLibrary();
        var manifestConfig = CreateManifestConfig([library]);

        var task = CreateHarvestTask(
            manifestConfig,
            closure: ClosureWithSinglePrimary(CoreLibrary),
            plannerResult: PlannerResultWithSinglePrimary(CoreLibrary));

        await task.RunAsync(repo.BuildContext);

        // Both cross-RID receipts were invalidated; any subsequent Pack invocation requires
        // ConsolidateHarvest to regenerate them.
        await Assert.That(repo.Exists(manifestPath)).IsFalse();
        await Assert.That(repo.Exists(summaryPath)).IsFalse();

        // The consolidated license tree is also invalidated (existing cleanup).
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2/licenses/_consolidated/zlib/copyright")).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Planner_Produces_Zero_Primary_Files()
    {
        // G1 post-harvest invariant: even when the walker and the planner each return a
        // success-shaped result, the task must fail if the deployment produced zero primary
        // binaries. Defends against silent feature-flag degradation / partial vcpkg installs
        // that happen to pass the upstream guards.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        SeedVcpkgTripletLayout(repo);

        var library = ManifestFixture.CreateTestCoreLibrary();
        var manifestConfig = CreateManifestConfig([library]);

        var task = CreateHarvestTask(
            manifestConfig,
            closure: ClosureWithSinglePrimary(CoreLibrary),
            plannerResult: PlannerResultWithEmptyStatistics(CoreLibrary));

        var exception = await Assert.That(() => task.RunAsync(repo.BuildContext)).Throws<CakeException>();

        await Assert.That(exception!.Message).Contains("zero primary binaries");
        await Assert.That(exception.Message).Contains(CoreLibrary);

        // The error path writes a failure rid-status, never a success one.
        var statusFilePath = RidStatusPath(CoreLibrary, WindowsRid);
        await Assert.That(repo.Exists(statusFilePath)).IsTrue();
        var status = await DeserializeStatusAsync(repo, statusFilePath);
        await Assert.That(status!.Success).IsFalse();
    }

    private static Build.Tasks.Harvest.HarvestTask CreateHarvestTask(
        ManifestConfig manifestConfig,
        BinaryClosure closure,
        ArtifactPlannerResult plannerResult,
        VcpkgConfiguration? vcpkgConfiguration = null)
    {
        var mockWalker = Substitute.For<IBinaryClosureWalker>();
        mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns(ClosureResult.FromBinaryClosure(closure));

        var mockPlanner = Substitute.For<IArtifactPlanner>();
        mockPlanner.CreatePlanAsync(Arg.Any<LibraryManifest>(), Arg.Any<BinaryClosure>(), Arg.Any<DirectoryPath>(), Arg.Any<CancellationToken>())
            .Returns(plannerResult);

        var mockDeployer = Substitute.For<IArtifactDeployer>();
        mockDeployer.DeployArtifactsAsync(Arg.Any<DeploymentPlan>(), Arg.Any<CancellationToken>())
            .Returns(CopierResult.ToSuccess());

        var mockValidator = CreatePassingValidator();
        var runtimeProfile = CreateWindowsRuntimeProfile();

        return new Build.Tasks.Harvest.HarvestTask(
            new HarvestTaskRunner(
                mockWalker,
                mockPlanner,
                mockDeployer,
                mockValidator,
                runtimeProfile,
                manifestConfig),
            vcpkgConfiguration ?? new VcpkgConfiguration([], null));
    }

    private static BinaryClosure ClosureWithSinglePrimary(string libraryName)
    {
        // Shaped like a real hybrid-static harvest closure: exactly one primary binary,
        // no runtime deps in the node set (transitive deps are statically baked), and the
        // owning vcpkg package recorded in the Packages set.
        var primaryPath = new FilePath($"C:/vcpkg/installed/{WindowsTriplet}/bin/{libraryName}.dll");
        var primarySet = ImmutableHashSet.Create(primaryPath);
        var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            libraryName.ToLowerInvariant(),
        };

        return new BinaryClosure(primarySet, [], packages);
    }

    private static ArtifactPlannerResult PlannerResultWithSinglePrimary(string libraryName)
    {
        var primaryFile = new FileDeploymentInfo(
            new FilePath($"C:/vcpkg/installed/{WindowsTriplet}/bin/{libraryName}.dll"),
            libraryName.ToLowerInvariant(),
            DeploymentLocation.FileSystem);

        var stats = new DeploymentStatistics(
            libraryName,
            [primaryFile],
            [],
            [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { libraryName.ToLowerInvariant() },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            DeploymentStrategy.DirectCopy);

        var plan = new DeploymentPlan([], stats);
        return plan;
    }

    private static ArtifactPlannerResult PlannerResultWithEmptyStatistics(string libraryName)
    {
        var stats = new DeploymentStatistics(
            libraryName,
            [],
            [],
            [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            DeploymentStrategy.DirectCopy);

        var plan = new DeploymentPlan([], stats);
        return plan;
    }

    private static IDependencyPolicyValidator CreatePassingValidator()
    {
        var validator = Substitute.For<IDependencyPolicyValidator>();
        validator.Validate(Arg.Any<BinaryClosure>(), Arg.Any<LibraryManifest>())
            .Returns(ValidationResult.Pass());
        return validator;
    }

    private static IRuntimeProfile CreateWindowsRuntimeProfile()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.Rid.Returns(WindowsRid);
        profile.Triplet.Returns(WindowsTriplet);
        profile.PlatformFamily.Returns(PlatformFamily.Windows);
        profile.IsSystemFile(Arg.Any<FilePath>()).Returns(returnThis: false);
        return profile;
    }

    private static ManifestConfig CreateManifestConfig(IReadOnlyList<LibraryManifest> libraries)
    {
        var baseline = ManifestFixture.CreateTestManifestConfig();
        return baseline with
        {
            LibraryManifests = libraries.ToImmutableList(),
        };
    }

    private static string RidStatusPath(string libraryName, string rid)
    {
        return $"artifacts/harvest_output/{libraryName}/rid-status/{rid}.json";
    }

    private static async Task<RidHarvestStatus?> DeserializeStatusAsync(FakeRepoHandles repo, string relativePath)
    {
        var json = await repo.ReadAllTextAsync(relativePath);
        return JsonSerializer.Deserialize<RidHarvestStatus>(json, HarvestJsonContract.Options);
    }

    private static void SeedVcpkgTripletLayout(FakeRepoHandles repo)
    {
        WriteTextFile(repo, repo.ResolveFile($"vcpkg_installed/{WindowsTriplet}/bin/SDL2.dll"), "payload");
    }

    private static void WriteTextFile(FakeRepoHandles repo, FilePath path, string content)
    {
        var directory = repo.FileSystem.GetDirectory(path.GetDirectory());
        if (!directory.Exists)
        {
            directory.Create();
        }

        var file = repo.FileSystem.GetFile(path);
        using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }
}
