using Build.Host.Paths;
using Build.Targets.Package.Services;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Targets.Package.Services;

/// <summary>
/// <see cref="DotNetPackInvoker"/> exposes only <c>Pack</c>.
/// <c>Build</c> / <c>Restore</c> methods and the <c>buildProjectReferences</c> /
/// <c>FamilyVersionProperty</c> plumbing were retired when exact-pin
/// was replaced with a minimum-range dependency model.
/// </summary>
public sealed class DotNetPackInvokerTests
{
    [Test]
    public async Task Pack_Should_Pass_Version_And_NativePayloadSource_As_MSBuild_Globals()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithToolPath(new FilePath("C:/tools/dotnet.exe"));
        var pathService = Substitute.For<IPathService>();
        pathService.PackagesOutput.Returns(new DirectoryPath("C:/repo/artifacts/packages"));

        var invoker = new DotNetPackInvoker(world.CakeContext, world.Log, pathService);
        var invocation = new DotNetPackInvocation(
            Configuration: "Release",
            Version: "1.2.3",
            NativePayloadSource: new DirectoryPath("C:/repo/artifacts/harvest_output/SDL2"));

        var result = invoker.Pack(new FilePath("C:/repo/src/native/SDL2.Core.Native/SDL2.Core.Native.csproj"), invocation, noRestore: false, noBuild: false);

        await Assert.That(result.IsSuccess).IsTrue();
        var invocations = world.ProcessInvocations.Where(i => i.Command.GetFilename().FullPath == "dotnet.exe").ToList();
        await Assert.That(invocations.Count).IsEqualTo(1);
        await Assert.That(invocations[0].Arguments).Contains("1.2.3");
        await Assert.That(invocations[0].Arguments).Contains("NativePayloadSource=");
        await Assert.That(invocations[0].Arguments).Contains("harvest_output/SDL2");
    }

    [Test]
    public async Task Pack_Should_Omit_NativePayloadSource_When_Null()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithToolPath(new FilePath("C:/tools/dotnet.exe"));
        var pathService = Substitute.For<IPathService>();
        pathService.PackagesOutput.Returns(new DirectoryPath("C:/repo/artifacts/packages"));

        var invoker = new DotNetPackInvoker(world.CakeContext, world.Log, pathService);
        var invocation = new DotNetPackInvocation(
            Configuration: "Release",
            Version: "1.2.3",
            NativePayloadSource: null);

        var result = invoker.Pack(new FilePath("C:/repo/src/SDL2.Core/SDL2.Core.csproj"), invocation, noRestore: false, noBuild: false);

        await Assert.That(result.IsSuccess).IsTrue();
        var invocations = world.ProcessInvocations.Where(i => i.Command.GetFilename().FullPath == "dotnet.exe").ToList();
        await Assert.That(invocations.Count).IsEqualTo(1);
        await Assert.That(invocations[0].Arguments).DoesNotContain("NativePayloadSource=");
    }
}
