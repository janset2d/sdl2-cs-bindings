using Build.Features.Coverage;
using Build.Shared.Coverage;

namespace Build.Tests.Unit.Features.Coverage;

public class CoverageThresholdValidatorTests
{
    private static CoverageThresholdValidator CreateValidator() => new();

    private static CoverageBaseline Baseline(double lineMin = 60.0, double branchMin = 49.0) =>
        new() { LineCoverageMin = lineMin, BranchCoverageMin = branchMin };

    private static CoverageMetrics Metrics(double lineRate, double branchRate) =>
        new()
        {
            LineRate = lineRate,
            BranchRate = branchRate,
            LinesCovered = 0,
            LinesValid = 0,
            BranchesCovered = 0,
            BranchesValid = 0,
        };

    [Test]
    public async Task Validate_Should_Return_Success_When_Both_Metrics_Above_Thresholds()
    {
        var result = CreateValidator().Validate(
            Metrics(lineRate: 0.65, branchRate: 0.55),
            Baseline());

        await Assert.That(result.IsSuccess()).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Return_Success_When_Metrics_Exactly_Match_Thresholds()
    {
        var result = CreateValidator().Validate(
            Metrics(lineRate: 0.60, branchRate: 0.49),
            Baseline(lineMin: 60.0, branchMin: 49.0));

        await Assert.That(result.IsSuccess()).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Return_Violation_When_Line_Coverage_Below_Threshold()
    {
        var result = CreateValidator().Validate(
            Metrics(lineRate: 0.55, branchRate: 0.55),
            Baseline(lineMin: 60.0, branchMin: 49.0));

        await Assert.That(result.IsError()).IsTrue();

        var violation = (CoverageError)result;
        await Assert.That(violation).IsTypeOf<CoverageThresholdViolation>();

        var typed = (CoverageThresholdViolation)violation;
        await Assert.That(typed.Failures.Count).IsEqualTo(1);
        await Assert.That(typed.Failures[0]).Contains("Line");
    }

    [Test]
    public async Task Validate_Should_Return_Violation_When_Branch_Coverage_Below_Threshold()
    {
        var result = CreateValidator().Validate(
            Metrics(lineRate: 0.65, branchRate: 0.40),
            Baseline(lineMin: 60.0, branchMin: 49.0));

        await Assert.That(result.IsError()).IsTrue();

        var typed = (CoverageThresholdViolation)(CoverageError)result;
        await Assert.That(typed.Failures.Count).IsEqualTo(1);
        await Assert.That(typed.Failures[0]).Contains("Branch");
    }

    [Test]
    public async Task Validate_Should_Report_Both_Failures_When_Both_Metrics_Below_Thresholds()
    {
        var result = CreateValidator().Validate(
            Metrics(lineRate: 0.40, branchRate: 0.30),
            Baseline(lineMin: 60.0, branchMin: 49.0));

        await Assert.That(result.IsError()).IsTrue();

        var typed = (CoverageThresholdViolation)(CoverageError)result;
        await Assert.That(typed.Failures.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Validate_Should_Include_Measured_Percent_And_Floor_In_Failure_Messages()
    {
        var result = CreateValidator().Validate(
            Metrics(lineRate: 0.5532, branchRate: 0.4421),
            Baseline(lineMin: 60.0, branchMin: 49.0));

        var typed = (CoverageThresholdViolation)(CoverageError)result;

        await Assert.That(typed.Failures[0]).Contains("55.32");
        await Assert.That(typed.Failures[0]).Contains("60.00");
        await Assert.That(typed.Failures[1]).Contains("44.21");
        await Assert.That(typed.Failures[1]).Contains("49.00");
    }

    [Test]
    public async Task Validate_Should_Carry_Metrics_And_Baseline_Into_Success_Result()
    {
        var metrics = Metrics(lineRate: 0.65, branchRate: 0.55);
        var baseline = Baseline(lineMin: 60.0, branchMin: 49.0);

        var result = CreateValidator().Validate(metrics, baseline);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.CheckSuccess.Metrics).IsEqualTo(metrics);
        await Assert.That(result.CheckSuccess.Baseline).IsEqualTo(baseline);
    }

    [Test]
    public async Task Validate_Should_Carry_Metrics_And_Baseline_Into_Violation_Result()
    {
        var metrics = Metrics(lineRate: 0.40, branchRate: 0.30);
        var baseline = Baseline(lineMin: 60.0, branchMin: 49.0);

        var result = CreateValidator().Validate(metrics, baseline);
        var typed = (CoverageThresholdViolation)(CoverageError)result;

        await Assert.That(typed.Metrics).IsEqualTo(metrics);
        await Assert.That(typed.Baseline).IsEqualTo(baseline);
    }
}
