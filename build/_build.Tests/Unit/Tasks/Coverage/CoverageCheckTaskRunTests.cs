using Build.Modules.Coverage;
using Build.Modules.Coverage.Models;
using Build.Tasks.Coverage;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Tests.Unit.Tasks.Coverage;

public class CoverageCheckTaskRunTests
{
    [Test]
    public void Run_Should_Pass_When_Coverage_Meets_Baseline()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithCoverageBaseline(CreateCoverageBaseline(lineMin: 50.0, branchMin: 40.0))
            .WithCoberturaReport(CreateCoverageXml(lineRate: 0.60, branchRate: 0.50))
            .BuildContextWithHandles();

        var task = CreateTask(repo.FileSystem);

        task.Run(repo.BuildContext);
    }

    [Test]
    public async Task Run_Should_Throw_CakeException_When_Line_Coverage_Below_Baseline()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithCoverageBaseline(CreateCoverageBaseline(lineMin: 60.0, branchMin: 40.0))
            .WithCoberturaReport(CreateCoverageXml(lineRate: 0.55, branchRate: 0.50))
            .BuildContextWithHandles();

        var task = CreateTask(repo.FileSystem);

        await Assert.That(() => task.Run(repo.BuildContext)).Throws<CakeException>();
    }

    [Test]
    public async Task Run_Should_Throw_CakeException_When_Branch_Coverage_Below_Baseline()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithCoverageBaseline(CreateCoverageBaseline(lineMin: 40.0, branchMin: 50.0))
            .WithCoberturaReport(CreateCoverageXml(lineRate: 0.60, branchRate: 0.40))
            .BuildContextWithHandles();

        var task = CreateTask(repo.FileSystem);

        await Assert.That(() => task.Run(repo.BuildContext)).Throws<CakeException>();
    }

    [Test]
    public async Task Run_Should_Throw_InvalidOperationException_When_Coverage_File_Missing()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithCoverageBaseline(CreateCoverageBaseline(lineMin: 50.0, branchMin: 40.0))
            .BuildContextWithHandles();

        var task = CreateTask(repo.FileSystem);

        await Assert.That(() => task.Run(repo.BuildContext)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Run_Should_Throw_InvalidOperationException_When_Baseline_File_Missing()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithCoberturaReport(CreateCoverageXml(lineRate: 0.60, branchRate: 0.50))
            .BuildContextWithHandles();

        var task = CreateTask(repo.FileSystem);

        await Assert.That(() => task.Run(repo.BuildContext)).Throws<InvalidOperationException>();
    }

    [Test]
    public void Run_Should_Pass_When_Coverage_Exactly_Meets_Baseline()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithCoverageBaseline(CreateCoverageBaseline(lineMin: 60.0, branchMin: 49.0))
            .WithCoberturaReport(CreateCoverageXml(lineRate: 0.60, branchRate: 0.49))
            .BuildContextWithHandles();

        var task = CreateTask(repo.FileSystem);

        task.Run(repo.BuildContext);
    }

    private static CoverageCheckTask CreateTask(IFileSystem fileSystem)
    {
        return new CoverageCheckTask(
            new CoberturaReader(fileSystem),
            new CoverageBaselineReader(fileSystem),
            new CoverageThresholdValidator());
    }

    private static CoverageBaseline CreateCoverageBaseline(double lineMin, double branchMin)
    {
        return new CoverageBaseline
        {
            LineCoverageMin = lineMin,
            BranchCoverageMin = branchMin,
        };
    }

    private static string CreateCoverageXml(double lineRate, double branchRate)
    {
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"""
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <coverage line-rate="{lineRate}" branch-rate="{branchRate}" complexity="1" version="1.9" timestamp="1" lines-covered="1" lines-valid="2" branches-covered="1" branches-valid="2">
              <packages></packages>
            </coverage>
            """);
    }
}
