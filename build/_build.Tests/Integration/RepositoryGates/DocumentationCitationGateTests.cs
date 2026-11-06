using System.Text.RegularExpressions;
using Build.Tests.Fixtures;
using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Integration.RepositoryGates;

[GitWorkTreeOnly]
public sealed partial class DocumentationCitationGateTests
{
    [Test]
    public async Task NonMarkdownFiles_Should_Not_Cite_Documentation_Paths()
    {
        var baseline = GateBaseline.Load("doc-citations.baseline");
        var offenders = Citations().Except(baseline.Entries, StringComparer.Ordinal).ToArray();
        await Assert.That(offenders).IsEmpty().Because("\n" + string.Join('\n', offenders));
    }

    [Test]
    public async Task CitationBaseline_Should_List_Only_Citations_Still_Present()
    {
        var stale = GateBaseline.Load("doc-citations.baseline").Entries
            .Except(Citations(), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        await Assert.That(stale).IsEmpty().Because("Stale baseline entries:\n" + string.Join('\n', stale));
    }

    private static string[] Citations() => [.. RepositoryFiles.Tracked
        .Where(GateScopes.IsCitationScanned)
        .SelectMany(path => Citation().Matches(RepositoryFiles.ReadText(path) ?? string.Empty).Select(match => $"{path}|{match.Value}"))
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)];

    [GeneratedRegex("(?<![\\w./\\\\-])(?:docs[/\\\\][^\\s\"'<>)\\]]+|AGENTS\\.md|GLOSSARY\\.md)", RegexOptions.None, 1000)]
    private static partial Regex Citation();
}
