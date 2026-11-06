using Build.Targets.DumpbinDependents;
using Build.Tests.Fixtures;
using Cake.Core.Diagnostics;

namespace Build.Tests.Scenarios.DumpbinDependents;

public sealed class DumpbinDependentsTaskScenarios
{
    [Test]
    public async Task RunAsync_Should_Invoke_Dumpbin_When_Dll_Is_Provided()
    {
        var world = FakeCakeWorld.CreateWindows().WithDumpbinLibrary();

        var host = new TargetTestHost<DumpbinDependentsTask>(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.ProcessInvocations.Count).IsGreaterThanOrEqualTo(1);
        var dumpbinInv = world.ProcessInvocations[0];
        await Assert.That(dumpbinInv.Command.FullPath).Contains("dumpbin");
    }

    [Test]
    public async Task RunAsync_Should_Cancel_Before_Launching_Dumpbin()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var world = FakeCakeWorld.CreateWindows().WithDumpbinLibrary().WithCancellationToken(cancellation.Token);

        var result = await new TargetTestHost<DumpbinDependentsTask>(world).RunAsync();

        await Assert.That(result.Exception).IsAssignableTo<OperationCanceledException>();
        await Assert.That(((OperationCanceledException)result.Exception!).CancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(world.ProcessInvocations).IsEmpty();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Dll_Is_Missing()
    {
        var world = FakeCakeWorld.CreateWindows();

        var host = new TargetTestHost<DumpbinDependentsTask>(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("--dll");
    }

    [Test]
    public async Task RunAsync_Should_Warn_But_Not_Throw_When_Dll_File_Does_Not_Exist()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithProcessResult("dumpbin.exe", exitCode: 0, stdOut: "    (no deps)\n")
            .WithToolPath("C:/dumpbin/dumpbin.exe")
            .WithDll("artifacts/missing.dll");

        var host = new TargetTestHost<DumpbinDependentsTask>(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.Log.HasMessage(LogLevel.Warning, "File not found")).IsTrue();
    }
}
