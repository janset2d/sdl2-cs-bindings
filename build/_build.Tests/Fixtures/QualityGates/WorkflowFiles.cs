using System.Text.Json;
using System.Text.RegularExpressions;
using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Fixtures.QualityGates;

internal static partial class WorkflowFiles
{
    public static IEnumerable<string> Paths => RepositoryFiles.Tracked
        .Where(path => path.StartsWith(".github/", StringComparison.Ordinal)
            && (path.EndsWith(".yml", StringComparison.Ordinal) || path.EndsWith(".yaml", StringComparison.Ordinal)))
        .Order(StringComparer.Ordinal);

    public static IReadOnlySet<string> ManifestRunners
    {
        get
        {
            using var manifest = JsonDocument.Parse(RepositoryFiles.ReadText("build/manifest.json")!);
            return manifest.RootElement
                .GetProperty("runtimes")
                .EnumerateArray()
                .Select(runtime => runtime.GetProperty("runner").GetString()!)
                .ToHashSet(StringComparer.Ordinal);
        }
    }

    public static IReadOnlyList<RunnerLabel> FindRunnerLabels(string text) => RunnerToken()
        .Matches(text)
        .Select(match => new RunnerLabel(text.AsSpan(0, match.Index).Count('\n') + 1, match.Value))
        .ToArray();

    public static IReadOnlyList<ActionReference> FindActionReferences(string text) => ActionToken()
        .Matches(text)
        .Select(match => new ActionReference(text.AsSpan(0, match.Index).Count('\n') + 1, match.Groups["value"].Value))
        .ToArray();

    [GeneratedRegex("""(?m)^[ \t]*(?:-[ \t]+)?uses:[ \t]*(?:"(?<value>[^"\r\n]+)"|'(?<value>[^'\r\n]+)'|(?<value>[^\s#]+))""", RegexOptions.None, 1000)]
    private static partial Regex ActionToken();

    [GeneratedRegex(@"(?i)(?<![\w./:-])(?:ubuntu|windows|macos)-[A-Za-z0-9.-]+", RegexOptions.None, 1000)]
    private static partial Regex RunnerToken();
}
