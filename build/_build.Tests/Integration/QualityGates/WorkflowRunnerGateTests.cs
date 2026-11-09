using Build.Tests.Fixtures;
using Build.Tests.Fixtures.QualityGates;
using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Integration.QualityGates;

[GitWorkTreeOnly]
public sealed class WorkflowRunnerGateTests
{
    [Test]
    public async Task ThirdPartyActions_Should_Be_Pinned_By_Sha()
    {
        var references = WorkflowFiles.Paths.SelectMany(path =>
            WorkflowFiles.FindActionReferences(RepositoryFiles.ReadText(path)!)
                .Where(reference => !reference.Value.StartsWith("./", StringComparison.Ordinal)
                    && !reference.Value.StartsWith("actions/", StringComparison.Ordinal))
                .Select(reference => (Path: path, Reference: reference)))
            .ToArray();
        await Assert.That(references).IsNotEmpty();
        foreach (var (path, reference) in references)
        {
            await Assert.That(reference.Value)
                .Matches(@"\A(?:[A-Za-z0-9_.-]+/[A-Za-z0-9_./-]+@[0-9a-fA-F]{40}|docker://[^\s@]+@sha256:[0-9a-fA-F]{64})\z")
                .Because($"{path}:{reference.Line}");
        }
    }

    [Test]
    public async Task RunnerLabels_Should_Be_Manifest_Runners()
    {
        var runners = WorkflowFiles.ManifestRunners;
        var labels = WorkflowFiles.Paths.SelectMany(path =>
            WorkflowFiles.FindRunnerLabels(RepositoryFiles.ReadText(path)!)
                .Select(label => (Path: path, Label: label)))
            .ToArray();
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
                .Select(label => $"{path}:{label.Line}: {label.Value}")))
            .ToArray();
        await Assert.That(violations).IsEmpty();
    }
}
