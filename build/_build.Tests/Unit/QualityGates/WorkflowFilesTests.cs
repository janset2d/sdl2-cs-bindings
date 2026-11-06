using Build.Tests.Fixtures.QualityGates;

namespace Build.Tests.Unit.QualityGates;

public sealed class WorkflowFilesTests
{
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
        await Assert.That(WorkflowFiles.FindRunnerLabels(text))
            .IsEquivalentTo(new[] { new RunnerLabel(line, label) }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
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
