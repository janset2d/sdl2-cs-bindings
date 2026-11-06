using Build.Tests.Fixtures;
using Build.Tests.Fixtures.QualityGates;
using Build.Tests.Fixtures.RepositoryGates;
using TUnit.Assertions.Enums;

namespace Build.Tests.Integration.QualityGates;

[GitWorkTreeOnly]
public sealed class SuppressionLedgerTests
{
    [Test]
    public async Task Ledger_Should_Match_The_Baseline()
    {
        var actual = SuppressionLedger.Render(SuppressionLedger.ReadTracked());
        var expected = RepositoryFiles.ReadText(SuppressionLedger.BaselinePath);

        await Assert.That(expected).IsNotNull();
        await Assert.That(actual).IsEqualTo(expected.ReplaceLineEndings("\n"));
    }

    [Test]
    public async Task Ledger_Baseline_Should_List_Only_Entries_Still_Present()
    {
        var actual = SuppressionLedger.Render(SuppressionLedger.ReadTracked());
        var baseline = RepositoryFiles.ReadText(SuppressionLedger.BaselinePath) ?? throw new FileNotFoundException(SuppressionLedger.BaselinePath);
        var stale = SuppressionLedger.BaselineEntries(baseline)
            .Except(SuppressionLedger.BaselineEntries(actual), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        await Assert.That(stale).IsEmpty();
    }

    [Test]
    public async Task StagingMarkers_Should_Be_Gone()
    {
        var markers = SuppressionLedger.StagingMarkers(RepositoryFiles.ReadText(".editorconfig") ?? throw new FileNotFoundException(".editorconfig"));

        await Assert.That(markers).IsEmpty();
    }

    [Test]
    public async Task Ledger_Should_Count_All_Channels_In_Ordinal_Order()
    {
        var entries = SuppressionLedger.Read(SuppressionLedgerData.Files);

        await Assert.That(entries).IsEquivalentTo(SuppressionLedgerData.ExpectedEntries, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Ledger_Should_Render_Channel_Totals_And_Duplicate_Counts()
    {
        var rendered = SuppressionLedger.Render(SuppressionLedgerData.DuplicateEntries);

        await Assert.That(rendered).IsEqualTo(SuppressionLedgerData.ExpectedDuplicateRender);
    }

    [Test]
    public async Task Ledger_Should_Read_Conditional_Imports_And_Command_Line_Switches()
    {
        var entries = SuppressionLedger.Read(SuppressionLedgerData.SwitchFiles);

        await Assert.That(entries).IsEquivalentTo(SuppressionLedgerData.ExpectedSwitchEntries, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Ledger_Should_Distinguish_CSharp_Trivia_From_String_Data()
    {
        var entries = SuppressionLedger.Read(SuppressionLedgerData.SourceFiles);

        await Assert.That(entries).IsEquivalentTo(SuppressionLedgerData.ExpectedSourceEntries, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Ledger_Should_Count_Generated_Directories_Even_Without_A_Header()
    {
        var entries = SuppressionLedger.Read(SuppressionLedgerData.HeaderlessFiles);

        await Assert.That(entries).IsEquivalentTo(SuppressionLedgerData.ExpectedHeaderlessEntries, CollectionOrdering.Matching);
    }
}
