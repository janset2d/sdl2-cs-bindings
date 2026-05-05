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
    }

    // Additional scenario tests (dotnet failure, SDK version log assertion) deferred.
    // InfoPipeline uses AnsiConsole.Status() (Spectre.Console interactive spinner),
    // which requires a real terminal or per-test IAnsiConsole isolation. The Spectre
    // static facade races with other parallel tests that also call AnsiConsole.Write().
    // When InfoPipeline migrates to Targets/Info/ in P4, the spinner can be replaced
    // with ICakeLog-based progress output, making it fully scenario-testable.
}
