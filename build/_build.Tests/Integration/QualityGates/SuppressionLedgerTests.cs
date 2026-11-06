using Build.Tests.Fixtures;
using Build.Tests.Fixtures.QualityGates;

namespace Build.Tests.Integration.QualityGates;

[GitWorkTreeOnly]
[ClassDataSource<SuppressionLedgerSession>(Shared = SharedType.PerTestSession)]
public sealed class SuppressionLedgerTests(SuppressionLedgerSession session)
{
    [Test]
    public async Task Ledger_Should_Match_The_Baseline()
    {
        await Assert.That(session.Added).IsEmpty().Because(string.Join(Environment.NewLine, session.Added));
    }

    [Test]
    public async Task Ledger_Baseline_Should_List_Only_Entries_Still_Present()
    {
        await Assert.That(session.Stale).IsEmpty().Because(string.Join(Environment.NewLine, session.Stale));
    }

    [Test]
    public async Task StagingMarkers_Should_Be_Gone()
    {
        var markers = session.Entries.Where(entry => entry.StartsWith("staging-marker ", StringComparison.Ordinal));

        await Assert.That(markers).IsEmpty();
    }
}
