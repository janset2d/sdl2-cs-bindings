using System.Text.Json;
using System.Text.RegularExpressions;
using Build.Tests.Fixtures;
using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Integration.QualityGates;

[GitWorkTreeOnly]
public sealed partial class WorkflowRunnerGateTests
{
    [Test]
    public async Task ThirdPartyActions_Should_Be_Pinned_By_Sha()
    {
        var references = new List<string>();
        var violations = new List<string>();
        foreach (var path in GitHubPaths().Where(path => path.EndsWith(".yml", StringComparison.Ordinal)
                     || path.EndsWith(".yaml", StringComparison.Ordinal)))
        {
            var text = RepositoryFiles.ReadText(path)!;
            foreach (Match match in ActionUses().Matches(text))
            {
                var value = match.Groups["value"].Value;
                if (value.StartsWith("./", StringComparison.Ordinal) || value.StartsWith("actions/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var location = $"{path}:{text.AsSpan(0, match.Index).Count('\n') + 1}: {value}";
                references.Add(location);
                var pin = value.StartsWith("docker://", StringComparison.Ordinal) ? ImageDigest() : ActionCommit();
                if (!pin.IsMatch(value))
                {
                    violations.Add(location);
                }
            }
        }

        await Assert.That(references).IsNotEmpty();
        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task WorkflowRunnerLabels_Should_Be_Manifest_Runners()
    {
        using var manifest = ReadManifest();
        var runners = manifest.RootElement.GetProperty("runtimes").EnumerateArray()
            .Select(runtime => runtime.GetProperty("runner").GetString()!).ToArray();

        var labels = WorkflowLabels().ToArray();
        await Assert.That(labels).IsNotEmpty();

        foreach (var label in labels)
        {
            await Assert.That(runners).Contains(label.Value).Because(label.Key);
        }
    }

    [Test]
    public async Task RunsOnLiterals_Should_Appear_Only_In_The_Runner_Bootstrap()
    {
        using var manifest = ReadManifest();
        await Assert.That(RepositoryFiles.ReadText(".github/workflows/runners.yml")).IsNotNull();
        var utility = manifest.RootElement.GetProperty("ci_runners").GetProperty("utility").GetString();
        var runner = manifest.RootElement.GetProperty("runtimes").EnumerateArray()
            .Single(runtime => runtime.GetProperty("rid").GetString() == utility).GetProperty("runner").GetString();
        var literals = WorkflowLabels(runsOnOnly: true)
            .Where(label => label.Key != ".github/workflows/build-linux-container.yml")
            .Select(label => $"{label.Key}|{label.Value}").ToArray();
        string[] expected = [$".github/workflows/runners.yml|{runner}"];

        await Assert.That(literals).IsNotEmpty();
        await Assert.That(literals).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task RunnerLabels_Should_Not_Use_Latest_Aliases()
    {
        var violations = new List<string>();
        foreach (var path in GitHubPaths().Append("build/manifest.json"))
        {
            var text = RepositoryFiles.ReadText(path)!;
            violations.AddRange(RunnerLabelScan.FindLatestAliases(text)
                .Select(match => $"{path}:{text.AsSpan(0, match.Index).Count('\n') + 1}: {match.Value}"));
        }

        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task ManifestRunnerLabels_Should_Appear_Only_In_The_Runner_Bootstrap()
    {
        using var manifest = ReadManifest();
        var runners = manifest.RootElement.GetProperty("runtimes").EnumerateArray()
            .Select(runtime => runtime.GetProperty("runner").GetString()!).ToHashSet(StringComparer.Ordinal);
        var labels = new List<string>();
        var locations = new List<string>();
        foreach (var path in GitHubPaths().Where(path => path != ".github/workflows/build-linux-container.yml"))
        {
            var text = RepositoryFiles.ReadText(path)!;
            foreach (var match in RunnerLabelScan.FindManifestLabels(text, runners))
            {
                labels.Add($"{path}|{match.Value}");
                locations.Add($"{path}:{text.AsSpan(0, match.Index).Count('\n') + 1}: {match.Value}");
            }
        }

        var utility = manifest.RootElement.GetProperty("ci_runners").GetProperty("utility").GetString();
        var runner = manifest.RootElement.GetProperty("runtimes").EnumerateArray()
            .Single(runtime => runtime.GetProperty("rid").GetString() == utility).GetProperty("runner").GetString();
        string[] expected = [$".github/workflows/runners.yml|{runner}"];
        await Assert.That(labels).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching)
            .Because(string.Join(Environment.NewLine, locations));
    }

    [Test]
    public async Task CiRunnerLegs_Should_Name_Manifest_Runtimes()
    {
        using var manifest = ReadManifest();
        await Assert.That(manifest.RootElement.TryGetProperty("ci_runners", out var ci)).IsTrue()
            .Because("build/manifest.json must name the CI utility and OS legs");
        var rids = manifest.RootElement.GetProperty("runtimes").EnumerateArray()
            .Select(runtime => runtime.GetProperty("rid").GetString()!).ToArray();

        await Assert.That(rids).Contains(ci.GetProperty("utility").GetString()!);

        var legs = ci.GetProperty("os_legs").EnumerateArray().Select(leg => leg.GetString()!).ToArray();
        await Assert.That(legs).IsNotEmpty();
        await Assert.That(legs.Distinct(StringComparer.Ordinal).Count()).IsEqualTo(legs.Length);

        foreach (var leg in legs)
        {
            await Assert.That(rids).Contains(leg);
        }

        string[] expected = ["linux", "osx", "win"];
        await Assert.That(legs.Select(leg => leg.Split('-')[0]).Order(StringComparer.Ordinal).ToArray())
            .IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static JsonDocument ReadManifest() => JsonDocument.Parse(RepositoryFiles.ReadText("build/manifest.json")!);

    private static IEnumerable<string> GitHubPaths() => RepositoryFiles.Tracked
        .Where(path => path.StartsWith(".github/", StringComparison.Ordinal)).Order(StringComparer.Ordinal);

    private static IEnumerable<KeyValuePair<string, string>> WorkflowLabels(bool runsOnOnly = false)
    {
        var paths = RepositoryFiles.Tracked.Where(path => path.StartsWith(".github/", StringComparison.Ordinal)
            && (path.EndsWith(".yml", StringComparison.Ordinal) || path.EndsWith(".yaml", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            var text = RepositoryFiles.ReadText(path)!;
            var matches = RunnerField().Matches(text).Where(match => !runsOnOnly || match.Groups["key"].Value == "runs-on");

            if (!runsOnOnly)
            {
                matches = matches.Concat(RunnerDefault().Matches(text));
            }

            foreach (var match in matches)
            {
                var value = match.Groups["value"].Value.Trim();

                if (value.Length == 0 || value.Contains("${{", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var label in value.Trim('[', ']').Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    yield return new KeyValuePair<string, string>(path, label.Trim('\'', '"'));
                }
            }
        }
    }

    [GeneratedRegex(@"(?m)^[ \t]*(?:-[ \t]+)?(?<key>runs-on|runner):[ \t]*(?<value>\[[^\]]*\]|[^\r\n#]*)", RegexOptions.None, 1000)]
    private static partial Regex RunnerField();

    [GeneratedRegex(@"(?m)^[ \t]*(?:-[ \t]+)?uses:[ \t]*['""]?(?<value>[^ \t\r\n'""#]+)", RegexOptions.None, 1000)]
    private static partial Regex ActionUses();

    [GeneratedRegex(@"^[^@]+@[0-9a-fA-F]{40}$", RegexOptions.None, 1000)]
    private static partial Regex ActionCommit();

    [GeneratedRegex(@"^docker://[^@]+@sha256:[0-9a-fA-F]{64}$", RegexOptions.None, 1000)]
    private static partial Regex ImageDigest();

    [GeneratedRegex(@"(?m)^[ \t]+runner:[ \t]*\r?\n(?:[ \t]+(?:type|required):[^\r\n]*\r?\n)*[ \t]+default:[ \t]*(?<value>[^\r\n#]+)", RegexOptions.None, 1000)]
    private static partial Regex RunnerDefault();
}

internal static partial class RunnerLabelScan
{
    public static Match[] FindLatestAliases(string text) => LatestAlias().Matches(text).ToArray();

    public static Match[] FindManifestLabels(string text, IReadOnlySet<string> labels) => Regex.Matches(text,
        @"(?<![A-Za-z0-9.-])(?:" + string.Join('|', labels.Select(Regex.Escape)) + @")(?![A-Za-z0-9.-])",
        RegexOptions.None, TimeSpan.FromSeconds(1)).ToArray();

    [GeneratedRegex(@"(?i)\b(?:ubuntu|windows|macos)-[A-Za-z0-9.-]*latest[A-Za-z0-9.-]*", RegexOptions.None, 1000)]
    private static partial Regex LatestAlias();
}
