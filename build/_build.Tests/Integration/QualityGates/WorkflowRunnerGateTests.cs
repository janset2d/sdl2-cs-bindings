using System.Text.Json;
using System.Text.RegularExpressions;
using Build.Tests.Fixtures;
using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Integration.QualityGates;

[GitWorkTreeOnly]
public sealed partial class WorkflowRunnerGateTests
{
    [Test]
    public async Task WorkflowRunnerLabels_Should_Be_Manifest_Runners()
    {
        using var manifest = ReadManifest();
        var runners = manifest.RootElement.GetProperty("runtimes").EnumerateArray()
            .Select(runtime => runtime.GetProperty("runner").GetString()!).ToArray();

        foreach (var label in WorkflowLabels())
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

        await Assert.That(literals).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task RunnerLabels_Should_Not_Use_Latest_Aliases()
    {
        using var manifest = ReadManifest();
        var labels = WorkflowLabels().Concat(manifest.RootElement.GetProperty("runtimes").EnumerateArray()
            .Select(runtime => new KeyValuePair<string, string>("build/manifest.json", runtime.GetProperty("runner").GetString()!)));

        foreach (var label in labels)
        {
            await Assert.That(label.Value.EndsWith("-latest", StringComparison.Ordinal)).IsFalse()
                .Because($"{label.Key}: {label.Value}");
        }
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

        foreach (var leg in ci.GetProperty("os_legs").EnumerateArray())
        {
            await Assert.That(rids).Contains(leg.GetString()!);
        }
    }

    private static JsonDocument ReadManifest() => JsonDocument.Parse(RepositoryFiles.ReadText("build/manifest.json")!);

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

    [GeneratedRegex(@"(?m)^[ \t]+runner:[ \t]*\r?\n(?:[ \t]+(?:type|required):[^\r\n]*\r?\n)*[ \t]+default:[ \t]*(?<value>[^\r\n#]+)", RegexOptions.None, 1000)]
    private static partial Regex RunnerDefault();
}
