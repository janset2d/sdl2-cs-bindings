using Build.Host;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Host;

public sealed class BuildContextCancellationTokenTests
{
    [Test]
    public async Task CancellationToken_Should_Return_Injected_Token_When_Set_Via_FakeCakeWorld()
    {
        using var cts = new CancellationTokenSource();
        var world = FakeCakeWorld.CreateWindows().WithCancellationToken(cts.Token);

        var buildContext = world.CreateBuildContext();

        await Assert.That(buildContext.CancellationToken).IsEqualTo(cts.Token);
    }

    [Test]
    public async Task CancellationToken_Should_Default_To_None_When_Not_Set()
    {
        var world = FakeCakeWorld.CreateWindows();

        var buildContext = world.CreateBuildContext();

        await Assert.That(buildContext.CancellationToken).IsEqualTo(CancellationToken.None);
    }
}
