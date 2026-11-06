using Build.Targets.OtoolAnalyze;
using Build.Tests.Fixtures;

namespace Build.Tests.Scenarios.OtoolAnalyze;

public sealed class OtoolAnalyzeTaskScenarios
{
    [Test]
    public async Task RunAsync_Should_Render_Analysis_Section_When_Dll_Is_Provided()
    {
        var world = FakeCakeWorld.CreateOsx().WithOtoolLibrary();

        var host = CreateHost(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.AnsiConsole.Output).Contains("Analyzing Specific Libraries");
        await Assert.That(world.AnsiConsole.Output).Contains("libSDL2");
    }

    [Test]
    public async Task RunAsync_Should_Propagate_Cancellation_Without_PerFile_Errors()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var world = FakeCakeWorld.CreateOsx().WithOtoolLibrary().WithCancellationToken(cancellation.Token);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Exception).IsAssignableTo<OperationCanceledException>();
        await Assert.That(((OperationCanceledException)result.Exception!).CancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(world.ProcessInvocations).IsEmpty();
        await Assert.That(world.Log.ErrorCount).IsEqualTo(0);
    }

    [Test]
    public async Task RunAsync_Should_Walk_Vcpkg_When_Dll_Is_Empty_And_Triplet_Exists()
    {
        var world = FakeCakeWorld.CreateOsx()
            .WithProcessResult("otool", exitCode: 0,
                stdOut: "vcpkg_installed/x64-osx-dynamic/lib/libfoo.dylib:\n\t/usr/lib/libSystem.B.dylib (compatibility version 1.0.0, current version 1319.100.3)\n")
            .WithToolPath("/usr/bin/otool")
            .WithTextFile("vcpkg_installed/x64-osx-dynamic/lib/libfoo.dylib", "binary");

        var host = CreateHost(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.AnsiConsole.Output).Contains("Analyzing Vcpkg Libraries");
        await Assert.That(world.AnsiConsole.Output).Contains("Found vcpkg libraries for triplet");
    }

    [Test]
    public async Task RunAsync_Should_Warn_When_No_Vcpkg_Triplet_Exists()
    {
        var world = FakeCakeWorld.CreateOsx();

        var host = CreateHost(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.AnsiConsole.Output).Contains("Vcpkg lib directory not found");
    }

    [Test]
    public async Task RunAsync_Should_Warn_When_Vcpkg_Triplet_Has_No_Dylibs()
    {
        var world = FakeCakeWorld.CreateOsx()
            .WithTextFile("vcpkg_installed/x64-osx-dynamic/lib/.placeholder", "");

        var host = CreateHost(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.AnsiConsole.Output).Contains("No .dylib files found");
    }

    private static TargetTestHost<OtoolAnalyzeTask> CreateHost(FakeCakeWorld world)
    {
        return new TargetTestHost<OtoolAnalyzeTask>(world)
            .WithServices(services => services.AddOtoolAnalyzeTarget());
    }
}
