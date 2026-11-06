using Build.Tests.Fixtures.QualityGates;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.QualityGates;

public sealed class SuppressionLedgerParserTests
{
    [Test]
    public async Task Read_Should_Count_Ignore_Trivia_Without_Reason_In_The_Value()
    {
        var entries = SuppressionLedger.Read(SuppressionLedgerParserData.IgnoreTrivia);

        await Assert.That(entries).IsEquivalentTo(SuppressionLedgerParserData.ExpectedIgnoreEntries, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Read_Should_Keep_The_Adjacent_Conditional_Import_Reason()
    {
        var entries = SuppressionLedger.Read([("z.props", SuppressionLedgerParserData.ConditionalImport)]);

        await Assert.That(entries).IsEquivalentTo([SuppressionLedgerParserData.ExpectedConditionalImport], CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(SuppressionLedgerParserData), nameof(SuppressionLedgerParserData.BroadSuppressions))]
    public async Task Read_Should_Refuse_Assembly_And_Module_Suppressions(string content)
    {
        var error = await Assert.That(() => SuppressionLedger.Read([("z.cs", content)])).Throws<InvalidOperationException>();

        await Assert.That(error!.Message).IsEqualTo(SuppressionLedgerParserData.BroadSuppressionError);
    }

    [Test]
    public async Task Read_Should_Count_Slopwatch_Disables_Severities_And_Excludes()
    {
        var entries = SuppressionLedger.Read(SuppressionLedgerParserData.SlopwatchFiles);

        await Assert.That(entries).IsEquivalentTo(SuppressionLedgerParserData.ExpectedSlopwatchEntries, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Read_Should_Refuse_An_Unknown_Slopwatch_File()
    {
        var error = await Assert.That(() => SuppressionLedger.Read([(".slopwatch/custom.json", "{}")])).Throws<InvalidOperationException>();

        await Assert.That(error!.Message).IsEqualTo(".slopwatch/custom.json: unknown Slopwatch configuration is not an inventoried suppression channel.");
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

    [Test]
    [MethodDataSource(typeof(SuppressionLedgerParserData), nameof(SuppressionLedgerParserData.ConfigEntries))]
    public async Task Read_Should_Count_Config_Values_And_Adjacent_Reasons(string content, string expected)
    {
        var entries = SuppressionLedger.Read([(".editorconfig", content)]);

        await Assert.That(entries).IsEquivalentTo([expected], CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(SuppressionLedgerParserData), nameof(SuppressionLedgerParserData.InvalidConfigs))]
    public async Task Read_Should_Refuse_An_Invalid_Config_Line(string content, string expected)
    {
        var error = await Assert.That(() => SuppressionLedger.Read([(".editorconfig", content)])).Throws<InvalidOperationException>();

        await Assert.That(error!.Message).IsEqualTo(expected);
    }

    [Test]
    [MethodDataSource(typeof(SuppressionLedgerParserData), nameof(SuppressionLedgerParserData.HiddenProjectWarnings))]
    public async Task Read_Should_Refuse_Hidden_Project_Warning_Lists(string content, string expected)
    {
        var error = await Assert.That(() => SuppressionLedger.Read([("z.csproj", content)])).Throws<InvalidOperationException>();

        await Assert.That(error!.Message).IsEqualTo(expected);
    }
}
