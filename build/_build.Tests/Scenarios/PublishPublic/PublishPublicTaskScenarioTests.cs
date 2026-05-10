using Build.Targets.PublishPublic;
using Build.Tests.Fixtures;

namespace Build.Tests.Scenarios.PublishPublic;

/// <summary>
/// Coverage for the <c>PublishPublic</c> stub. PD-7 will replace the body with the real
/// nuget.org promotion implementation; until then the task surface should fail-loud with a
/// clear "not implemented yet" message so an operator stumbling --target PublishPublic sees
/// the stub status instead of a silent no-op.
/// </summary>
public sealed class PublishPublicTaskScenarioTests
{
    [Test]
    public async Task RunAsync_Should_Throw_CakeException_With_Not_Implemented_Message()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var host = new TargetTestHostV2<PublishPublicTask>(world);

        var result = await host.RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("not implemented yet", StringComparison.Ordinal);
    }
}
