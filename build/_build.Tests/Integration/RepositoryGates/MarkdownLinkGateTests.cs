using System.Globalization;
using Build.Tests.Fixtures;
using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Integration.RepositoryGates;

[GitWorkTreeOnly]
public sealed class MarkdownLinkGateTests
{
    [Test]
    public async Task TrackedMarkdown_Should_Resolve_Every_Relative_Link()
    {
        var baseline = GateBaseline.Load("broken-links.baseline");
        var offenders = BrokenLinks()
            .Where(link => !baseline.Entries.Contains(link.Entry))
            .Select(link => link.Diagnostic)
            .ToArray();
        await Assert.That(offenders).IsEmpty().Because("\n" + string.Join('\n', offenders));
    }

    [Test]
    public async Task LinkBaseline_Should_List_Only_Links_That_Are_Still_Broken()
    {
        var broken = BrokenLinks().Select(link => link.Entry).ToHashSet(StringComparer.Ordinal);
        var stale = GateBaseline.Load("broken-links.baseline").Entries
            .Except(broken, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        await Assert.That(stale).IsEmpty().Because("Stale baseline entries:\n" + string.Join('\n', stale));
    }

    [Test]
    public async Task Resolves_Should_Return_False_When_Case_Differs()
    {
        await Assert.That(RepositoryFiles.Resolves("readme.md")).IsFalse();
        await Assert.That(RepositoryFiles.Resolves("README.md")).IsTrue();
    }

    private static List<(string Entry, string Diagnostic)> BrokenLinks()
    {
        var broken = new List<(string Entry, string Diagnostic)>();

        foreach (var source in RepositoryFiles.Tracked.Where(GateScopes.IsLinkScanned).Order(StringComparer.Ordinal))
        {
            foreach (var link in MarkdownLinks.Extract(RepositoryFiles.ReadText(source) ?? string.Empty))
            {
                var target = MarkdownLinks.ToRepositoryPath(source, link.Target);

                if (target is null || RepositoryFiles.Resolves(target))
                {
                    continue;
                }

                var entry = $"{source}|{target}";
                broken.Add((entry, string.Create(CultureInfo.InvariantCulture, $"{source}:{link.Line} -> {target}\n{entry}")));
            }
        }

        return broken;
    }
}
