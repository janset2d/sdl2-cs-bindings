using Build.Targets.LddDependents;
using Build.Tests.Fixtures;
using Cake.Core.Diagnostics;

namespace Build.Tests.Scenarios.LddDependents;

public sealed class LddDependentsTaskScenarios
{
    [Test]
    public async Task RunAsync_Should_Invoke_Ldd_When_Dll_Is_Provided()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithProcessResult("ldd", exitCode: 0,
                stdOut: "    libc.so.6 => /lib/x86_64-linux-gnu/libc.so.6 (0x00007f1234560000)\n")
            .WithToolPath("/usr/bin/ldd")
            .WithDll("artifacts/libSDL2.so")
            .WithTextFile("artifacts/libSDL2.so", "binary content");

        var host = new TargetTestHost<LddDependentsTask>(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.ProcessInvocations.Count).IsGreaterThanOrEqualTo(1);
        var lddInv = world.ProcessInvocations[0];
        await Assert.That(lddInv.Command.FullPath).Contains("ldd");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Dll_Is_Missing()
    {
        var world = FakeCakeWorld.CreateLinux();

        var host = new TargetTestHost<LddDependentsTask>(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("--dll");
    }

    [Test]
    public async Task RunAsync_Should_Warn_But_Not_Throw_When_Dll_File_Does_Not_Exist()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithProcessResult("ldd", exitCode: 0,
                stdOut: "    libc.so.6 => /lib/x86_64-linux-gnu/libc.so.6 (0x00007f1234560000)\n")
            .WithToolPath("/usr/bin/ldd")
            .WithDll("artifacts/missing.so");

        var host = new TargetTestHost<LddDependentsTask>(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.Log.HasMessage(LogLevel.Warning, "File not found")).IsTrue();
    }
}
