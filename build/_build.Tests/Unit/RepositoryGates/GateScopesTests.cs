using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Unit.RepositoryGates;

public sealed class GateScopesTests
{
    [Test]
    public async Task DateGateScope_Should_Exclude_Superpowers_Tree()
    {
        await Assert.That(GateScopes.RequiresDate("docs/superpowers/plans/2026-01-01-x.md")).IsFalse();
        await Assert.That(GateScopes.RequiresDate("docs/adr/0001-x.md")).IsTrue();
        await Assert.That(GateScopes.RequiresDate("GLOSSARY.md")).IsFalse();
    }

    [Test]
    public async Task CitationScope_Should_Exclude_Markdown_And_Gate_Sources()
    {
        await Assert.That(GateScopes.IsCitationScanned("a.md")).IsFalse();
        await Assert.That(GateScopes.IsCitationScanned(
            "build/_build.Tests/Integration/RepositoryGates/doc-citations.baseline")).IsFalse();
        await Assert.That(GateScopes.IsCitationScanned(
            "build/_build.Tests/Fixtures/RepositoryGates/MarkdownLinks.cs")).IsFalse();
        await Assert.That(GateScopes.IsCitationScanned(
            "build/_build.Tests/Unit/RepositoryGates/MarkdownLinksTests.cs")).IsFalse();
        await Assert.That(GateScopes.IsCitationScanned("a.verified.cs")).IsFalse();
        await Assert.That(GateScopes.IsCitationScanned("Directory.Packages.props")).IsTrue();
    }

    [Test]
    public async Task CompanionPathScope_Should_Include_Public_Canon_Only()
    {
        await Assert.That(GateScopes.IsCompanionPathScanned("build/AGENTS.md")).IsTrue();
        await Assert.That(GateScopes.IsCompanionPathScanned("docs/engineering/testing.md")).IsTrue();
        await Assert.That(GateScopes.IsCompanionPathScanned("docs/superpowers/plans/x.md")).IsFalse();
        await Assert.That(GateScopes.IsCompanionPathScanned("spikes/README.md")).IsFalse();
    }
}
