using Build.Modules.Harvesting;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;

namespace Build.Tests.Unit.ArtifactDeployer;

public class DeploymentExecutionTests
{
    private FakeFileSystem _fakeFs = null!;
    private FakeEnvironment _env = null!;
    private ICakeContext _ctx = null!;

    [Before(Test)]
    public void SetUp()
    {
        _env = FakeEnvironment.CreateWindowsEnvironment();
        _fakeFs = new FakeFileSystem(_env);
        _ctx = Substitute.For<ICakeContext>();
        _ctx.Log.Returns(new FakeLog());
        _ctx.Environment.Returns(_env);
        _ctx.FileSystem.Returns(_fakeFs);
    }

    [Test]
    public async Task DeployArtifactsAsync_Should_Return_Success_When_No_Actions()
    {
        var deployer = new Build.Modules.Harvesting.ArtifactDeployer(_ctx);

        var emptyStats = new DeploymentStatistics(
            "SDL2_image", [], [], [], new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase), DeploymentStrategy.DirectCopy);
        var plan = new DeploymentPlan([], emptyStats);

        var result = await deployer.DeployArtifactsAsync(plan);

        await Assert.That(result.IsSuccess()).IsTrue();
    }

    [Test]
    public async Task DeployArtifactsAsync_Should_Copy_File_To_Target_Path()
    {
        // Create source file in fake filesystem
        var sourcePath = new FilePath("C:/vcpkg/bin/SDL2_image.dll");
        var targetPath = new FilePath("C:/output/runtimes/win-x64/native/SDL2_image.dll");
        _fakeFs.CreateFile(sourcePath);

        var actions = new List<DeploymentAction>
        {
            new FileCopyAction(sourcePath, targetPath, "sdl2-image", ArtifactOrigin.Primary)
        };

        var stats = new DeploymentStatistics(
            "SDL2_image",
            [new FileDeploymentInfo(sourcePath, "sdl2-image", DeploymentLocation.FileSystem)],
            [], [], new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2-image" }, new HashSet<string>(StringComparer.OrdinalIgnoreCase), DeploymentStrategy.DirectCopy);
        var plan = new DeploymentPlan(actions, stats);

        var deployer = new Build.Modules.Harvesting.ArtifactDeployer(_ctx);
        var result = await deployer.DeployArtifactsAsync(plan);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(_fakeFs.Exist(targetPath)).IsTrue();
    }

    [Test]
    public async Task DeployArtifactsAsync_Should_Create_Target_Directory_If_Missing()
    {
        var sourcePath = new FilePath("C:/vcpkg/bin/SDL2.dll");
        var targetDir = new DirectoryPath("C:/output/runtimes/win-x64/native");
        var targetPath = targetDir.CombineWithFilePath("SDL2.dll");
        _fakeFs.CreateFile(sourcePath);

        var actions = new List<DeploymentAction>
        {
            new FileCopyAction(sourcePath, targetPath, "sdl2", ArtifactOrigin.Primary)
        };

        var stats = new DeploymentStatistics(
            "SDL2", [new FileDeploymentInfo(sourcePath, "sdl2", DeploymentLocation.FileSystem)],
            [], [], new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2" }, new HashSet<string>(StringComparer.OrdinalIgnoreCase), DeploymentStrategy.DirectCopy);
        var plan = new DeploymentPlan(actions, stats);

        var deployer = new Build.Modules.Harvesting.ArtifactDeployer(_ctx);
        var result = await deployer.DeployArtifactsAsync(plan);

        await Assert.That(result.IsSuccess()).IsTrue();
        // Target directory should have been created
        await Assert.That(_fakeFs.Exist(targetDir)).IsTrue();
    }

    [Test]
    public async Task DeployArtifactsAsync_Should_Copy_Multiple_Files_In_Sequence()
    {
        var source1 = new FilePath("C:/vcpkg/bin/SDL2_image.dll");
        var source2 = new FilePath("C:/vcpkg/bin/zlib1.dll");
        var target1 = new FilePath("C:/output/native/SDL2_image.dll");
        var target2 = new FilePath("C:/output/native/zlib1.dll");

        _fakeFs.CreateFile(source1);
        _fakeFs.CreateFile(source2);

        var actions = new List<DeploymentAction>
        {
            new FileCopyAction(source1, target1, "sdl2-image", ArtifactOrigin.Primary),
            new FileCopyAction(source2, target2, "zlib", ArtifactOrigin.Runtime)
        };

        var stats = new DeploymentStatistics(
            "SDL2_image",
            [new FileDeploymentInfo(source1, "sdl2-image", DeploymentLocation.FileSystem)],
            [new FileDeploymentInfo(source2, "zlib", DeploymentLocation.FileSystem)],
            [], new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2-image", "zlib" }, new HashSet<string>(StringComparer.OrdinalIgnoreCase), DeploymentStrategy.DirectCopy);
        var plan = new DeploymentPlan(actions, stats);

        var deployer = new Build.Modules.Harvesting.ArtifactDeployer(_ctx);
        var result = await deployer.DeployArtifactsAsync(plan);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(_fakeFs.Exist(target1)).IsTrue();
        await Assert.That(_fakeFs.Exist(target2)).IsTrue();
    }

    [Test]
    public async Task DeployArtifactsAsync_Should_Copy_License_Files()
    {
        var sourceBinary = new FilePath("C:/vcpkg/bin/SDL2_image.dll");
        var sourceLicense = new FilePath("C:/vcpkg/share/sdl2-image/copyright");
        var targetBinary = new FilePath("C:/output/native/SDL2_image.dll");
        var targetLicense = new FilePath("C:/output/licenses/sdl2-image/copyright");

        _fakeFs.CreateFile(sourceBinary);
        _fakeFs.CreateFile(sourceLicense);

        var actions = new List<DeploymentAction>
        {
            new FileCopyAction(sourceBinary, targetBinary, "sdl2-image", ArtifactOrigin.Primary),
            new FileCopyAction(sourceLicense, targetLicense, "sdl2-image", ArtifactOrigin.License)
        };

        var stats = new DeploymentStatistics(
            "SDL2_image",
            [new FileDeploymentInfo(sourceBinary, "sdl2-image", DeploymentLocation.FileSystem)],
            [],
            [new FileDeploymentInfo(sourceLicense, "sdl2-image", DeploymentLocation.FileSystem)],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2-image" }, new HashSet<string>(StringComparer.OrdinalIgnoreCase), DeploymentStrategy.DirectCopy);
        var plan = new DeploymentPlan(actions, stats);

        var deployer = new Build.Modules.Harvesting.ArtifactDeployer(_ctx);
        var result = await deployer.DeployArtifactsAsync(plan);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(_fakeFs.Exist(targetBinary)).IsTrue();
        await Assert.That(_fakeFs.Exist(targetLicense)).IsTrue();
    }
}
