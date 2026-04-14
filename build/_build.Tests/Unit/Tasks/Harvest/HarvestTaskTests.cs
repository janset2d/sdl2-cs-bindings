using System.Collections.Immutable;
using System.Text.Json;
using Build.Context.Models;
using Build.Models;
using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;
using IOPath = System.IO.Path;

namespace Build.Tests.Unit.Tasks.Harvest;

public class HarvestTaskTests
{
    [Test]
    public async Task RunAsync_Should_Generate_Success_Rid_Status_File_When_Harvest_Completes()
    {
        var harvestRoot = CreateTempHarvestOutputRoot();
        try
        {
            var library = CreateLibraryManifest("SDL2", "sdl2", isCore: true);
            var manifestConfig = CreateManifestConfig([library]);

            var mockWalker = Substitute.For<IBinaryClosureWalker>();
            mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
                .Returns(CreateClosureResult());

            var mockPlanner = Substitute.For<IArtifactPlanner>();
            mockPlanner.CreatePlanAsync(Arg.Any<LibraryManifest>(), Arg.Any<BinaryClosure>(), Arg.Any<DirectoryPath>(), Arg.Any<CancellationToken>())
                .Returns(CreatePlannerResult("SDL2"));

            var mockDeployer = Substitute.For<IArtifactDeployer>();
            mockDeployer.DeployArtifactsAsync(Arg.Any<DeploymentPlan>(), Arg.Any<CancellationToken>())
                .Returns(CopierResult.ToSuccess());

            var runtimeProfile = CreateRuntimeProfile();
            var task = new Build.Tasks.Harvest.HarvestTask(mockWalker, mockPlanner, mockDeployer, runtimeProfile, manifestConfig);
            var context = TaskTestHelpers.CreateBuildContext(new DirectoryPath(harvestRoot), []);

            await task.RunAsync(context);

            var statusFilePath = IOPath.Combine(harvestRoot, "SDL2", "rid-status", "win-x64.json");
            await Assert.That(File.Exists(statusFilePath)).IsTrue();

            var statusJson = await File.ReadAllTextAsync(statusFilePath);
            var status = JsonSerializer.Deserialize<RidHarvestStatus>(statusJson);

            await Assert.That(status).IsNotNull();
            await Assert.That(status!.LibraryName).IsEqualTo("SDL2");
            await Assert.That(status.Success).IsTrue();
            await Assert.That(status.ErrorMessage).IsNull();
            await Assert.That(status.Statistics).IsNotNull();
            await Assert.That(status.Statistics!.DeploymentStrategy).IsEqualTo("DirectCopy");
        }
        finally
        {
            TaskTestHelpers.DeleteDirectoryQuietly(harvestRoot);
        }
    }

    [Test]
    public async Task RunAsync_Should_Generate_Error_Rid_Status_File_When_Closure_Fails()
    {
        var harvestRoot = CreateTempHarvestOutputRoot();
        try
        {
            var library = CreateLibraryManifest("SDL2", "sdl2", isCore: true);
            var manifestConfig = CreateManifestConfig([library]);

            var mockWalker = Substitute.For<IBinaryClosureWalker>();
            mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
                .Returns(new ClosureError("dependency scan failed"));

            var mockPlanner = Substitute.For<IArtifactPlanner>();
            var mockDeployer = Substitute.For<IArtifactDeployer>();

            var runtimeProfile = CreateRuntimeProfile();
            var task = new Build.Tasks.Harvest.HarvestTask(mockWalker, mockPlanner, mockDeployer, runtimeProfile, manifestConfig);
            var context = TaskTestHelpers.CreateBuildContext(new DirectoryPath(harvestRoot), []);

            var thrown = false;
            try
            {
                await task.RunAsync(context);
            }
            catch (CakeException ex)
            {
                thrown = true;
                await Assert.That(ex.Message).Contains("Binary closure failed");
            }

            await Assert.That(thrown).IsTrue();

            var statusFilePath = IOPath.Combine(harvestRoot, "SDL2", "rid-status", "win-x64.json");
            await Assert.That(File.Exists(statusFilePath)).IsTrue();

            var statusJson = await File.ReadAllTextAsync(statusFilePath);
            var status = JsonSerializer.Deserialize<RidHarvestStatus>(statusJson);

            await Assert.That(status).IsNotNull();
            await Assert.That(status!.Success).IsFalse();
            await Assert.That(status.ErrorMessage).IsEqualTo("Harvest failed for SDL2");
            await Assert.That(status.Statistics).IsNull();
        }
        finally
        {
            TaskTestHelpers.DeleteDirectoryQuietly(harvestRoot);
        }
    }

    [Test]
    public async Task RunAsync_Should_Harvest_Only_Specified_Libraries_From_Vcpkg_Options()
    {
        var harvestRoot = CreateTempHarvestOutputRoot();
        try
        {
            var core = CreateLibraryManifest("SDL2", "sdl2", isCore: true);
            var satellite = CreateLibraryManifest("SDL2_image", "sdl2-image", isCore: false);
            var manifestConfig = CreateManifestConfig([core, satellite]);

            var mockWalker = Substitute.For<IBinaryClosureWalker>();
            mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
                .Returns(CreateClosureResult());

            var mockPlanner = Substitute.For<IArtifactPlanner>();
            mockPlanner.CreatePlanAsync(Arg.Any<LibraryManifest>(), Arg.Any<BinaryClosure>(), Arg.Any<DirectoryPath>(), Arg.Any<CancellationToken>())
                .Returns(CreatePlannerResult("SDL2_image"));

            var mockDeployer = Substitute.For<IArtifactDeployer>();
            mockDeployer.DeployArtifactsAsync(Arg.Any<DeploymentPlan>(), Arg.Any<CancellationToken>())
                .Returns(CopierResult.ToSuccess());

            var runtimeProfile = CreateRuntimeProfile();
            var task = new Build.Tasks.Harvest.HarvestTask(mockWalker, mockPlanner, mockDeployer, runtimeProfile, manifestConfig);
            var context = TaskTestHelpers.CreateBuildContext(new DirectoryPath(harvestRoot), ["SDL2_image"]);

            await task.RunAsync(context);

            var selectedStatus = IOPath.Combine(harvestRoot, "SDL2_image", "rid-status", "win-x64.json");
            var nonSelectedStatus = IOPath.Combine(harvestRoot, "SDL2", "rid-status", "win-x64.json");

            await Assert.That(File.Exists(selectedStatus)).IsTrue();
            await Assert.That(File.Exists(nonSelectedStatus)).IsFalse();
        }
        finally
        {
            TaskTestHelpers.DeleteDirectoryQuietly(harvestRoot);
        }
    }

    private static IRuntimeProfile CreateRuntimeProfile()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.Rid.Returns("win-x64");
        profile.Triplet.Returns("x64-windows-hybrid");
        profile.PlatformFamily.Returns(PlatformFamily.Windows);
        profile.IsSystemFile(Arg.Any<FilePath>()).Returns(returnThis: false);
        return profile;
    }

    private static ManifestConfig CreateManifestConfig(IReadOnlyList<LibraryManifest> libraries)
    {
        return new ManifestConfig
        {
            LibraryManifests = libraries.ToImmutableList(),
        };
    }

    private static LibraryManifest CreateLibraryManifest(string name, string vcpkgName, bool isCore)
    {
        return new LibraryManifest
        {
            Name = name,
            VcpkgName = vcpkgName,
            VcpkgVersion = "1.0.0",
            VcpkgPortVersion = 0,
            NativeLibName = $"{name}.Native",
            NativeLibVersion = "1.0.0.0",
            IsCoreLib = isCore,
            PrimaryBinaries =
            [
                new PrimaryBinary { Os = "Windows", Patterns = ImmutableList.Create("*.dll") },
                new PrimaryBinary { Os = "Linux", Patterns = ImmutableList.Create("*.so") },
                new PrimaryBinary { Os = "OSX", Patterns = ImmutableList.Create("*.dylib") },
            ],
        };
    }

    private static ClosureResult CreateClosureResult()
    {
        var closure = new BinaryClosure(
            ImmutableHashSet<FilePath>.Empty,
            [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        return closure;
    }

    private static ArtifactPlannerResult CreatePlannerResult(string libraryName)
    {
        var stats = new DeploymentStatistics(
            libraryName,
            [],
            [],
            [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { libraryName.ToLowerInvariant() },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            DeploymentStrategy.DirectCopy);

        var plan = new DeploymentPlan([], stats);
        return plan;
    }

    private static string CreateTempHarvestOutputRoot()
    {
        var path = IOPath.Combine(IOPath.GetTempPath(), "sdl2-bindings-tests", "harvest-task", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

}
