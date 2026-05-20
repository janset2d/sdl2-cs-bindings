namespace Build.Tests.Unit.Targets.GenerateBindings.Snapshots;

public sealed class VerifyConventionsTests
{
    [Test]
    public Task VerifyConventions_Should_Match_Project_Settings()
    {
        return VerifyChecks.Run();
    }
}
