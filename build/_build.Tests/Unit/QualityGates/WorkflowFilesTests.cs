using Build.Tests.Fixtures.QualityGates;

namespace Build.Tests.Unit.QualityGates;

public sealed class WorkflowFilesTests
{
    [Test]
    [Arguments("- uses: owner/repo@v1", "owner/repo@v1")]
    [Arguments("  uses: \"owner/repo@abc\"  # v1", "owner/repo@abc")]
    [Arguments("  uses: 'owner/repo@abc'", "owner/repo@abc")]
    [Arguments("  uses: owner/repo@abc # v1", "owner/repo@abc")]
    [Arguments("    uses: ./.github/workflows/x.yml", "./.github/workflows/x.yml")]
    [Arguments("- uses: docker://img@sha256:abc", "docker://img@sha256:abc")]
    public async Task FindActionReferences_Should_Find_Uses_Values(string text, string value)
    {
        ActionReference[] expected = [new(1, value)];
        await Assert.That(WorkflowFiles.FindActionReferences(text))
            .IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task FindActionReferences_Should_Return_References_In_Text_Order()
    {
        ActionReference[] expected = [new(2, "z/step@v1"), new(4, "a/workflow@v2")];
        await Assert.That(WorkflowFiles.FindActionReferences("steps:\r\n  - uses: z/step@v1\r\njob:\r\n  uses: a/workflow@v2"))
            .IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    public static IEnumerable<(string Text, int Line, string Label)> Labels() =>
    [
        ("runs-on:\n  - ubuntu-latest", 2, "ubuntu-latest"),
        ("runs-on:\n  labels: windows-latest", 2, "windows-latest"),
        ("matrix:\n  os: [ubuntu-latest]", 2, "ubuntu-latest"),
        ("description: x\n default: macos-latest", 2, "macos-latest"),
        ("runs-on: macos-latest-xlarge", 1, "macos-latest-xlarge"),
        ("runs-on: Ubuntu-Latest", 1, "Ubuntu-Latest"),
    ];

    [Test, MethodDataSource(nameof(Labels))]
    public async Task FindRunnerLabels_Should_Find_Labels_In_All_Yaml_Forms(string text, int line, string label)
    {
        RunnerLabel[] expected = [new(line, label)];
        await Assert.That(WorkflowFiles.FindRunnerLabels(text))
            .IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task FindRunnerLabels_Should_Return_Expression_Labels_In_Text_Order()
    {
        RunnerLabel[] expected = [new(1, "ubuntu-latest"), new(1, "ubuntu-24.04")];
        await Assert.That(WorkflowFiles.FindRunnerLabels("runs-on: ${{ cond && 'ubuntu-latest' || 'ubuntu-24.04' }}"))
            .IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("image: ghcr.io/x/y:focal-latest")]
    [Arguments("image: ghcr.io/x/ubuntu-latest\npath: ./windows-latest\nimage: x:macos-latest")]
    public async Task FindRunnerLabels_Should_Ignore_Image_References_And_Paths(string text)
    {
        await Assert.That(WorkflowFiles.FindRunnerLabels(text)).IsEmpty();
    }
}
