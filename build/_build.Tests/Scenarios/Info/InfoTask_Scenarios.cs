using Build.Features.Info;
using Build.Tests.Fixtures;

namespace Build.Tests.Scenarios.Info;

public sealed class InfoTask_Scenarios
{
    [Test]
    public async Task RunAsync_Should_Complete_Without_Exception_When_DotNet_Is_Available()
    {
        var world = FakeCakeWorldV2.Create(FakeRepoPlatformV2.Windows)
            .WithProcessResult("dotnet", exitCode: 0, stdOut: "10.0.203\n");

        var host = new TargetTestHostV2<InfoTask>(world)
            .WithServices(s => s.AddInfoFeature());

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

    // DotNet failure-path scenario deferred. InfoPipeline uses AnsiConsole.Status()
    // (Spectre.Console interactive spinner), which cannot run in two parallel test
    // instances without IAnsiConsole injection. The fix (constructor-inject IAnsiConsole,
    // Spectre.Console.Testing.TestConsole in FakeCakeWorldV2) is deferred to P4
    // alongside the InfoTask migration. See docs/refactoring/p2a-v2-test-infrastructure-
    // review-handoff.md §12 for the canonical fix plan.
}
