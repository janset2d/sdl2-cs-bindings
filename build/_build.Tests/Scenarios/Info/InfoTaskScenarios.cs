using Build.Targets.Info;
using Build.Tests.Fixtures;

namespace Build.Tests.Scenarios.Info;

public sealed class InfoTaskScenarios
{
    [Test]
    public async Task RunAsync_Should_Complete_Without_Exception_When_DotNet_Is_Available()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithProcessResult("dotnet", exitCode: 0, stdOut: "10.0.203\n");

        var host = new TargetTestHost<InfoTask>(world);

        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Exception).IsNull();

        await Assert.That(world.ProcessInvocations.Count)
            .IsGreaterThanOrEqualTo(1);
        var dotnetInv = world.ProcessInvocations[0];
        await Assert.That(dotnetInv.Command.FullPath).Contains("dotnet");
        await Assert.That(dotnetInv.Arguments).Contains("--version");
        await Assert.That(dotnetInv.RedirectStandardOutput).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Display_Error_When_DotNet_Returns_NonZero_ExitCode()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithProcessResult("dotnet", exitCode: 1, stdOut: "", stdErr: "SDK not found");

        var host = new TargetTestHost<InfoTask>(world);

        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Exception).IsNull();

        await Assert.That(world.AnsiConsole.Output).Contains("Failed (Exit Code: 1)");
    }
}
