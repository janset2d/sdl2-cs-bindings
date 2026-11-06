using Build.Tests.Fixtures;
using Build.Tests.Fixtures.QualityGates;
using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Integration.QualityGates;

[GitWorkTreeOnly]
public sealed class WorkflowRunnerGateTests
{
    [Test]
    public async Task RunnerLabels_Should_Be_Manifest_Runners()
    {
        var runners = WorkflowFiles.ManifestRunners;
        var labels = WorkflowFiles.Paths.SelectMany(path =>
            WorkflowFiles.FindRunnerLabels(RepositoryFiles.ReadText(path)!)
                .Select(label => (Path: path, Label: label))).ToArray();
        await Assert.That(labels).IsNotEmpty();
        foreach (var (path, label) in labels)
        {
            await Assert.That(runners).Contains(label.Value).Because($"{path}:{label.Line}");
        }
    }

    [Test]
    public async Task ManifestRunners_Should_Not_Use_Latest_Aliases()
    {
        var violations = WorkflowFiles.ManifestRunners.Where(runner => runner.Contains("latest", StringComparison.OrdinalIgnoreCase))
            .Select(runner => $"build/manifest.json: {runner}")
            .Concat(WorkflowFiles.Paths.SelectMany(path => WorkflowFiles.FindRunnerLabels(RepositoryFiles.ReadText(path)!)
                .Where(label => label.Value.Contains("latest", StringComparison.OrdinalIgnoreCase))
                .Select(label => $"{path}:{label.Line}: {label.Value}"))).ToArray();
        await Assert.That(violations).IsEmpty();
    }
}
