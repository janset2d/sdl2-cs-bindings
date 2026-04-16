using System.Collections.Immutable;
using System.Text.Json;
using Build.Context.Models;
using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Build.Modules.Strategy.Models;
using Build.Modules.Strategy.Results;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Tasks.Harvest;

public class HarvestTaskTests
{
    [Test]
    public async Task RunAsync_Should_Generate_Success_Rid_Status_File_When_Harvest_Completes()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

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

        var mockValidator = CreatePassingValidator();
        var runtimeProfile = CreateRuntimeProfile();
        var task = new Build.Tasks.Harvest.HarvestTask(mockWalker, mockPlanner, mockDeployer, mockValidator, runtimeProfile, manifestConfig);

    await task.RunAsync(repo.BuildContext);

    const string statusFilePath = "artifacts/harvest_output/SDL2/rid-status/win-x64.json";
    await Assert.That(repo.Exists(statusFilePath)).IsTrue();

    var statusJson = await repo.ReadAllTextAsync(statusFilePath);
        var status = JsonSerializer.Deserialize<RidHarvestStatus>(statusJson);

        await Assert.That(status).IsNotNull();
        await Assert.That(status!.LibraryName).IsEqualTo("SDL2");
        await Assert.That(status.Success).IsTrue();
        await Assert.That(status.ErrorMessage).IsNull();
        await Assert.That(status.Statistics).IsNotNull();
        await Assert.That(status.Statistics!.DeploymentStrategy).IsEqualTo("DirectCopy");
    }

    [Test]
    public async Task RunAsync_Should_Generate_Error_Rid_Status_File_When_Closure_Fails()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        var library = CreateLibraryManifest("SDL2", "sdl2", isCore: true);
        var manifestConfig = CreateManifestConfig([library]);

        var mockWalker = Substitute.For<IBinaryClosureWalker>();
        mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns(new ClosureBuildError("dependency scan failed"));

        var mockPlanner = Substitute.For<IArtifactPlanner>();
        var mockDeployer = Substitute.For<IArtifactDeployer>();
        var mockValidator = CreatePassingValidator();

        var runtimeProfile = CreateRuntimeProfile();
        var task = new Build.Tasks.Harvest.HarvestTask(mockWalker, mockPlanner, mockDeployer, mockValidator, runtimeProfile, manifestConfig);

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

    const string statusFilePath = "artifacts/harvest_output/SDL2/rid-status/win-x64.json";
    await Assert.That(repo.Exists(statusFilePath)).IsTrue();

    var statusJson = await repo.ReadAllTextAsync(statusFilePath);
        var status = JsonSerializer.Deserialize<RidHarvestStatus>(statusJson);

        await Assert.That(status).IsNotNull();
        await Assert.That(status!.Success).IsFalse();
        await Assert.That(status.ErrorMessage).IsEqualTo("Harvest failed for SDL2");
        await Assert.That(status.Statistics).IsNull();
    }

    [Test]
    public async Task RunAsync_Should_Generate_Error_Rid_Status_File_When_Operational_Exception_Occurs()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        var library = CreateLibraryManifest("SDL2", "sdl2", isCore: true);
        var manifestConfig = CreateManifestConfig([library]);

        var mockWalker = Substitute.For<IBinaryClosureWalker>();
        mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ClosureResult>(new InvalidOperationException("planner inputs were inconsistent")));

        var mockPlanner = Substitute.For<IArtifactPlanner>();
        var mockDeployer = Substitute.For<IArtifactDeployer>();
        var mockValidator = CreatePassingValidator();

        var runtimeProfile = CreateRuntimeProfile();
        var task = new Build.Tasks.Harvest.HarvestTask(mockWalker, mockPlanner, mockDeployer, mockValidator, runtimeProfile, manifestConfig);

        await Assert.That(() => task.RunAsync(repo.BuildContext)).Throws<InvalidOperationException>();

        const string statusFilePath = "artifacts/harvest_output/SDL2/rid-status/win-x64.json";
        await Assert.That(repo.Exists(statusFilePath)).IsTrue();

        var statusJson = await repo.ReadAllTextAsync(statusFilePath);
        var status = JsonSerializer.Deserialize<RidHarvestStatus>(statusJson);

        await Assert.That(status).IsNotNull();
        await Assert.That(status!.Success).IsFalse();
        await Assert.That(status.ErrorMessage).IsEqualTo("planner inputs were inconsistent");
    }

    [Test]
    public async Task RunAsync_Should_Propagate_Cancellation_Without_Generating_Error_Rid_Status_File()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        var library = CreateLibraryManifest("SDL2", "sdl2", isCore: true);
        var manifestConfig = CreateManifestConfig([library]);

        var mockWalker = Substitute.For<IBinaryClosureWalker>();
        mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ClosureResult>(new OperationCanceledException("harvest canceled")));

        var mockPlanner = Substitute.For<IArtifactPlanner>();
        var mockDeployer = Substitute.For<IArtifactDeployer>();
        var mockValidator = CreatePassingValidator();

        var runtimeProfile = CreateRuntimeProfile();
        var task = new Build.Tasks.Harvest.HarvestTask(mockWalker, mockPlanner, mockDeployer, mockValidator, runtimeProfile, manifestConfig);

        await Assert.That(() => task.RunAsync(repo.BuildContext)).Throws<OperationCanceledException>();

        const string statusFilePath = "artifacts/harvest_output/SDL2/rid-status/win-x64.json";
        await Assert.That(repo.Exists(statusFilePath)).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Harvest_Only_Specified_Libraries_From_Vcpkg_Options()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithLibraries("SDL2_image")
            .BuildContextWithHandles();

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

        var mockValidator = CreatePassingValidator();
        var runtimeProfile = CreateRuntimeProfile();
        var task = new Build.Tasks.Harvest.HarvestTask(mockWalker, mockPlanner, mockDeployer, mockValidator, runtimeProfile, manifestConfig);

        await task.RunAsync(repo.BuildContext);

        const string selectedStatus = "artifacts/harvest_output/SDL2_image/rid-status/win-x64.json";
        const string nonSelectedStatus = "artifacts/harvest_output/SDL2/rid-status/win-x64.json";

        await Assert.That(repo.Exists(selectedStatus)).IsTrue();
        await Assert.That(repo.Exists(nonSelectedStatus)).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Dependency_Policy_Validation_Fails()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        var library = CreateLibraryManifest("SDL2_image", "sdl2-image", isCore: false);
        var manifestConfig = CreateManifestConfig([library]);

        var mockWalker = Substitute.For<IBinaryClosureWalker>();
        mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns(CreateClosureResult());

        var mockPlanner = Substitute.For<IArtifactPlanner>();
        var mockDeployer = Substitute.For<IArtifactDeployer>();

        var mockValidator = Substitute.For<IDependencyPolicyValidator>();
        mockValidator.Validate(Arg.Any<BinaryClosure>(), Arg.Any<LibraryManifest>())
            .Returns(ValidationResult.Fail(
            [new BinaryNode(new FilePath("C:/vcpkg/bin/zlib1.dll"), "zlib", "sdl2-image")],
            "dependency leak detected"));

        var runtimeProfile = CreateRuntimeProfile();
        var task = new Build.Tasks.Harvest.HarvestTask(mockWalker, mockPlanner, mockDeployer, mockValidator, runtimeProfile, manifestConfig);

        await Assert.That(() => task.RunAsync(repo.BuildContext)).Throws<CakeException>();
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
            SchemaVersion = "2.1",
            PackagingConfig = new PackagingConfig
            {
                ValidationMode = ValidationMode.Strict,
                CoreLibrary = "sdl2",
            },
            Runtimes =
            [
                new RuntimeInfo
                {
                    Rid = "win-x64",
                    Triplet = "x64-windows-hybrid",
                    Strategy = "hybrid-static",
                    Runner = "windows-latest",
                    ContainerImage = null,
                },
            ],
            PackageFamilies =
            [
                new PackageFamilyConfig
                {
                    Name = "core",
                    TagPrefix = "core",
                    ManagedProject = "src/SDL2.Core/SDL2.Core.csproj",
                    NativeProject = "src/native/SDL2.Core.Native/SDL2.Core.Native.csproj",
                    LibraryRef = "SDL2",
                    DependsOn = [],
                    ChangePaths = ["src/SDL2.Core/**", "src/native/SDL2.Core.Native/**"],
                },
            ],
            SystemExclusions = new SystemArtefactsConfig
            {
                Windows = new WindowsSystemArtefacts(),
                Linux = new LinuxSystemArtefacts(),
                Osx = new OsxSystemArtefacts(),
            },
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

    private static IDependencyPolicyValidator CreatePassingValidator()
    {
        var validator = Substitute.For<IDependencyPolicyValidator>();
        validator.Validate(Arg.Any<BinaryClosure>(), Arg.Any<LibraryManifest>())
            .Returns(ValidationResult.Pass());
        return validator;
    }

}
