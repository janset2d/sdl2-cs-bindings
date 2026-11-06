namespace Build.Tests.Fixtures;

internal static class TargetCancellationAssertions
{
    public static async Task AssertCancelledAsync(TargetRunResult result, FakeCakeWorld world, CancellationToken token)
    {
        await Assert.That(result.Exception).IsAssignableTo<OperationCanceledException>();
        await Assert.That(((OperationCanceledException)result.Exception!).CancellationToken).IsEqualTo(token);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(world.ProcessInvocations).IsEmpty();
    }
}
