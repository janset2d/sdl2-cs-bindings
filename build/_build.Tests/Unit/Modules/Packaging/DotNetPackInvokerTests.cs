using Build.Modules.Contracts;
using Build.Modules.Packaging;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;
using OneOf.Monads;

namespace Build.Tests.Unit.Modules.Packaging;

/// <summary>
/// Post-S1 (2026-04-17): <see cref="DotNetPackInvoker"/> exposes only <c>Pack</c>.
/// <c>Build</c> / <c>Restore</c> methods and the <c>buildProjectReferences</c> /
/// <c>FamilyVersionProperty</c> plumbing were retired when Mechanism 3 exact-pin
/// was replaced with SkiaSharp-style minimum range.
/// </summary>
public sealed class DotNetPackInvokerTests
{
    [Test]
    public async Task Pack_Should_Pass_Version_And_NativePayloadSource_As_MSBuild_Globals()
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithProcessCapture(out var capture)
            .WithToolPath(new FilePath("C:/tools/dotnet.exe"))
            .Build();

        var pathService = Substitute.For<IPathService>();
        pathService.PackagesOutput.Returns(new DirectoryPath("C:/repo/artifacts/packages"));

        var invoker = new DotNetPackInvoker(context, context.Log, pathService);
        var invocation = new DotNetPackInvocation(
            Configuration: "Release",
            Version: "1.2.3",
            NativePayloadSource: new DirectoryPath("C:/repo/artifacts/harvest_output/SDL2"));

        var result = invoker.Pack(new FilePath("C:/repo/src/native/SDL2.Core.Native/SDL2.Core.Native.csproj"), invocation, noRestore: false, noBuild: false);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(capture.Settings).IsNotNull();

        var renderedArgs = capture.Settings!.Arguments.Render();
        await Assert.That(renderedArgs).Contains("1.2.3");
        await Assert.That(renderedArgs).Contains("MinVerSkip=true");
        await Assert.That(renderedArgs).Contains("NativePayloadSource=");
        await Assert.That(renderedArgs).Contains("harvest_output/SDL2");
    }

    [Test]
    public async Task Pack_Should_Omit_NativePayloadSource_When_Null()
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithProcessCapture(out var capture)
            .WithToolPath(new FilePath("C:/tools/dotnet.exe"))
            .Build();

        var pathService = Substitute.For<IPathService>();
        pathService.PackagesOutput.Returns(new DirectoryPath("C:/repo/artifacts/packages"));

        var invoker = new DotNetPackInvoker(context, context.Log, pathService);
        var invocation = new DotNetPackInvocation(
            Configuration: "Release",
            Version: "1.2.3",
            NativePayloadSource: null);

        var result = invoker.Pack(new FilePath("C:/repo/src/SDL2.Core/SDL2.Core.csproj"), invocation, noRestore: false, noBuild: false);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(capture.Settings).IsNotNull();

        var renderedArgs = capture.Settings!.Arguments.Render();
        await Assert.That(renderedArgs).DoesNotContain("NativePayloadSource=");
    }
}
