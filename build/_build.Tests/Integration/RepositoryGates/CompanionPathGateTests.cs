using System.Text.RegularExpressions;
using Build.Tests.Fixtures;
using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Integration.RepositoryGates;

[GitWorkTreeOnly]
public sealed partial class CompanionPathGateTests
{
    [Test]
    public async Task PublicCanon_Should_Not_Cite_Companion_Repository_Paths()
    {
        var offenders = RepositoryFiles.Tracked.Where(GateScopes.IsCompanionPathScanned).Order(StringComparer.Ordinal)
            .SelectMany(path => CompanionPath().Matches(RepositoryFiles.ReadText(path) ?? string.Empty)
                .Select(match => $"{path} -> {match.Value}")).ToArray();
        await Assert.That(offenders).IsEmpty().Because("\n" + string.Join('\n', offenders));
    }

    [GeneratedRegex(@"(?i)sdl2-cs-bindings-internal[/\\]|(?i)\binternal\b[^\n]{0,40}docs/(research|archive|agents|plans)/", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex CompanionPath();
}
