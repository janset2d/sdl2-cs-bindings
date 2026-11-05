using Build.Tests.Integration.QualityGates;

namespace Build.Tests.Unit.QualityGates;

public sealed class RunnerLabelScanTests
{
    public static IEnumerable<(string Text, string Label)> Labels() =>
    [
        ("runs-on:\n  - ubuntu-latest", "ubuntu-latest"),
        ("runs-on:\n  labels: windows-latest", "windows-latest"),
        ("matrix:\n  os: [ubuntu-latest]", "ubuntu-latest"),
        ("runs-on: ${{ cond && 'ubuntu-latest' || 'ubuntu-24.04' }}", "ubuntu-latest"),
        ("description: x\n default: macos-latest", "macos-latest"),
        ("runs-on: macos-latest-xlarge", "macos-latest-xlarge"),
        ("runs-on: Ubuntu-Latest", "Ubuntu-Latest"),
    ];

    [Test, MethodDataSource(nameof(Labels))]
    public async Task FindLatestAliases_Should_Find_Labels_In_All_Yaml_Forms(string text, string label)
    {
        await Assert.That(RunnerLabelScan.FindLatestAliases(text).Select(match => match.Value).ToArray())
            .IsEquivalentTo(new[] { label }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test, MethodDataSource(nameof(Labels))]
    public async Task FindManifestLabels_Should_Find_Literals_In_All_Yaml_Forms(string text, string label)
    {
        await Assert.That(RunnerLabelScan.FindManifestLabels(text, new HashSet<string>(StringComparer.Ordinal) { label })
                .Select(match => match.Value).ToArray())
            .IsEquivalentTo(new[] { label }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task FindLatestAliases_Should_Ignore_Container_Image_Tags()
    {
        await Assert.That(RunnerLabelScan.FindLatestAliases("image: ghcr.io/x/y:focal-latest")).IsEmpty();
    }

    [Test]
    public async Task FindManifestLabels_Should_Return_Exact_Labels_In_Text_Order()
    {
        const string text = "labels: [ubuntu-24.04-arm, ubuntu-24.04]\n# ubuntu-24.04\nimage: ubuntu-24.04-extra";
        IReadOnlySet<string> labels = new HashSet<string>(StringComparer.Ordinal) { "ubuntu-24.04", "ubuntu-24.04-arm" };
        string[] expected = ["ubuntu-24.04-arm", "ubuntu-24.04", "ubuntu-24.04"];
        await Assert.That(RunnerLabelScan.FindManifestLabels(text, labels).Select(match => match.Value).ToArray())
            .IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
