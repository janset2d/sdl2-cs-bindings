using System.Text.RegularExpressions;
using Build.Tests.Fixtures;
using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Integration.RepositoryGates;

[GitWorkTreeOnly]
public sealed partial class DocumentDateGateTests
{
    [Test]
    public async Task CanonDocuments_Should_Carry_Date_Line()
    {
        var baseline = GateBaseline.Load("missing-date.baseline");
        var offenders = MissingDates().Except(baseline.Entries, StringComparer.Ordinal).ToArray();
        await Assert.That(offenders).IsEmpty().Because("\n" + string.Join('\n', offenders));
    }

    [Test]
    public async Task DateBaseline_Should_List_Only_Documents_Still_Missing_Date()
    {
        var stale = GateBaseline.Load("missing-date.baseline").Entries.Except(MissingDates(), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        await Assert.That(stale).IsEmpty().Because("Stale baseline entries:\n" + string.Join('\n', stale));
    }

    private static string[] MissingDates() => RepositoryFiles.Tracked.Where(path => GateScopes.RequiresDate(path)
        && !RepositoryFiles.ReadText(path).Split('\n').Take(10).Any(line => DateLine().IsMatch(line)))
        .Order(StringComparer.Ordinal).ToArray();

    [GeneratedRegex(@"^Date: \d{4}-\d{2}-\d{2}\s*$", RegexOptions.None, 1000)]
    private static partial Regex DateLine();
}
