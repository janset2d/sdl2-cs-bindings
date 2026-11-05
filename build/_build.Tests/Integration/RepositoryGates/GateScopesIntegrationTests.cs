using Build.Tests.Fixtures;
using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Integration.RepositoryGates;

[GitWorkTreeOnly]
public sealed class GateScopesIntegrationTests
{
    [Test]
    public async Task CitationScope_Should_Include_Tracked_Text_Files()
    {
        await Assert.That(GateScopes.IsCitationScanned("Directory.Packages.props")).IsTrue();
    }
}
