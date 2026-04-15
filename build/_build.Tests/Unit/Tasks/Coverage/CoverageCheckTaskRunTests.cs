using Build.Modules.Coverage;
using Build.Tasks.Coverage;
using Build.Tests.Fixtures;
using Cake.Core;
using IOPath = System.IO.Path;

namespace Build.Tests.Unit.Tasks.Coverage;

public class CoverageCheckTaskRunTests : TempDirectoryTestBase
{
    [Test]
    public async Task Run_Should_Pass_When_Coverage_Meets_Baseline()
    {
        var repoRoot = CreateTrackedTempDirectory("coverage-task-pass");
        await WriteBaselineAsync(repoRoot, lineMin: 50.0, branchMin: 40.0);
        await WriteCoverageAsync(repoRoot, lineRate: 0.60, branchRate: 0.50);

        var task = CreateTask();
        var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);

        task.Run(context);
    }

    [Test]
    public async Task Run_Should_Throw_CakeException_When_Line_Coverage_Below_Baseline()
    {
        var repoRoot = CreateTrackedTempDirectory("coverage-task-line-fail");
        await WriteBaselineAsync(repoRoot, lineMin: 60.0, branchMin: 40.0);
        await WriteCoverageAsync(repoRoot, lineRate: 0.55, branchRate: 0.50);

        var task = CreateTask();
        var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);

        await Assert.That(() => task.Run(context)).Throws<CakeException>();
    }

    [Test]
    public async Task Run_Should_Throw_CakeException_When_Branch_Coverage_Below_Baseline()
    {
        var repoRoot = CreateTrackedTempDirectory("coverage-task-branch-fail");
        await WriteBaselineAsync(repoRoot, lineMin: 40.0, branchMin: 50.0);
        await WriteCoverageAsync(repoRoot, lineRate: 0.60, branchRate: 0.40);

        var task = CreateTask();
        var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);

        await Assert.That(() => task.Run(context)).Throws<CakeException>();
    }

    [Test]
    public async Task Run_Should_Throw_InvalidOperationException_When_Coverage_File_Missing()
    {
        var repoRoot = CreateTrackedTempDirectory("coverage-task-no-coverage");
        await WriteBaselineAsync(repoRoot, lineMin: 50.0, branchMin: 40.0);
        // Intentionally no coverage file

        var task = CreateTask();
        var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);

        await Assert.That(() => task.Run(context)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Run_Should_Throw_InvalidOperationException_When_Baseline_File_Missing()
    {
        var repoRoot = CreateTrackedTempDirectory("coverage-task-no-baseline");
        await WriteCoverageAsync(repoRoot, lineRate: 0.60, branchRate: 0.50);
        // Intentionally no baseline file

        var task = CreateTask();
        var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);

        await Assert.That(() => task.Run(context)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Run_Should_Pass_When_Coverage_Exactly_Meets_Baseline()
    {
        var repoRoot = CreateTrackedTempDirectory("coverage-task-exact-match");
        await WriteBaselineAsync(repoRoot, lineMin: 60.0, branchMin: 49.0);
        await WriteCoverageAsync(repoRoot, lineRate: 0.60, branchRate: 0.49);

        var task = CreateTask();
        var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);

        task.Run(context);
    }

    private static CoverageCheckTask CreateTask()
    {
        var fileSystem = new Cake.Core.IO.FileSystem();
        return new CoverageCheckTask(new CoberturaReader(fileSystem), new CoverageBaselineReader(fileSystem));
    }

    private static async Task WriteBaselineAsync(string repoRoot, double lineMin, double branchMin)
    {
        var buildDir = IOPath.Combine(repoRoot, "build");
        Directory.CreateDirectory(buildDir);

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            line_coverage_min = lineMin,
            branch_coverage_min = branchMin,
        });

        await File.WriteAllTextAsync(IOPath.Combine(buildDir, "coverage-baseline.json"), json);
    }

    private static async Task WriteCoverageAsync(string repoRoot, double lineRate, double branchRate)
    {
        var coverageDir = IOPath.Combine(repoRoot, "artifacts", "test-results", "build-tests");
        Directory.CreateDirectory(coverageDir);

        var xml = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"""
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <coverage line-rate="{lineRate}" branch-rate="{branchRate}" complexity="1" version="1.9" timestamp="1" lines-covered="1" lines-valid="2" branches-covered="1" branches-valid="2">
              <packages></packages>
            </coverage>
            """);

        await File.WriteAllTextAsync(IOPath.Combine(coverageDir, "coverage.cobertura.xml"), xml);
    }
}
