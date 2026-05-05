using Build.Results;

namespace Build.Tests.Unit.Results;

/// <summary>
/// Tests for the <see cref="ValidationCheck"/>, <see cref="ValidationSeverity"/>, and
/// <see cref="ValidationReport"/> primitives — multi-check policy validation output.
/// </summary>
public sealed class ValidationReportTests
{
    // ───────────────────────────────────────────────────────────────────────
    //  ValidationCheck
    // ───────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ValidationCheck_Constructor_Should_Throw_When_Name_Is_Empty()
    {
        await Assert.That(() => new ValidationCheck(string.Empty, ValidationSeverity.Error, "msg"))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ValidationCheck_Constructor_Should_Throw_When_Message_Is_Empty()
    {
        await Assert.That(() => new ValidationCheck("rule", ValidationSeverity.Error, string.Empty))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ValidationCheck_Should_Allow_Null_Code()
    {
        var check = new ValidationCheck("rule", ValidationSeverity.Warning, "msg");

        await Assert.That(check.Name).IsEqualTo("rule");
        await Assert.That(check.Severity).IsEqualTo(ValidationSeverity.Warning);
        await Assert.That(check.Message).IsEqualTo("msg");
        await Assert.That(check.Code).IsNull();
    }

    // ───────────────────────────────────────────────────────────────────────
    //  ValidationReport
    // ───────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Empty_Should_Have_Zero_Count_And_Be_Valid()
    {
        var report = ValidationReport.Empty;

        await Assert.That(report.Count).IsEqualTo(0);
        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.HasWarnings).IsFalse();
    }

    [Test]
    public async Task Constructor_Should_Build_Report_From_Single_Check()
    {
        var check = new ValidationCheck("rule", ValidationSeverity.Warning, "msg");
        var report = new ValidationReport([check]);

        await Assert.That(report.Count).IsEqualTo(1);
    }

    [Test]
    public async Task IsValid_Should_Be_True_When_Only_Warnings_Present()
    {
        var report = new ValidationReport(
        [
            new ValidationCheck("rule", ValidationSeverity.Warning, "warn"),
        ]);

        await Assert.That(report.IsValid).IsTrue();
    }

    [Test]
    public async Task IsValid_Should_Be_False_When_Any_Error_Present()
    {
        var report = new ValidationReport(
        [
            new ValidationCheck("rule1", ValidationSeverity.Warning, "warn"),
            new ValidationCheck("rule2", ValidationSeverity.Error, "err"),
        ]);

        await Assert.That(report.IsValid).IsFalse();
    }

    [Test]
    public async Task HasWarnings_Should_Be_True_When_Warning_Present()
    {
        var report = new ValidationReport(
        [
            new ValidationCheck("rule", ValidationSeverity.Warning, "warn"),
        ]);

        await Assert.That(report.HasWarnings).IsTrue();
    }

    [Test]
    public async Task HasWarnings_Should_Be_False_When_Only_Errors_Present()
    {
        var report = new ValidationReport(
        [
            new ValidationCheck("rule", ValidationSeverity.Error, "err"),
        ]);

        await Assert.That(report.HasWarnings).IsFalse();
    }

    [Test]
    public async Task Errors_Should_Project_Only_Error_Severity_Checks()
    {
        var warn = new ValidationCheck("warn", ValidationSeverity.Warning, "w");
        var err = new ValidationCheck("err", ValidationSeverity.Error, "e");
        var report = new ValidationReport([warn, err]);

        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0]).IsEqualTo(err);
    }

    [Test]
    public async Task Warnings_Should_Project_Only_Warning_Severity_Checks()
    {
        var warn = new ValidationCheck("warn", ValidationSeverity.Warning, "w");
        var err = new ValidationCheck("err", ValidationSeverity.Error, "e");
        var report = new ValidationReport([warn, err]);

        await Assert.That(report.Warnings.Count).IsEqualTo(1);
        await Assert.That(report.Warnings[0]).IsEqualTo(warn);
    }

    [Test]
    public async Task Combine_Should_Merge_Reports_In_Order()
    {
        var a = new ValidationReport([new ValidationCheck("a", ValidationSeverity.Warning, "wa")]);
        var b = new ValidationReport([new ValidationCheck("b", ValidationSeverity.Error, "eb")]);

        var combined = ValidationReport.Combine(a, b);

        await Assert.That(combined.Count).IsEqualTo(2);
        var checks = combined.ToList();
        await Assert.That(checks[0].Name).IsEqualTo("a");
        await Assert.That(checks[1].Name).IsEqualTo("b");
    }

    [Test]
    public async Task Combine_Should_Return_Empty_When_All_Inputs_Empty()
    {
        var combined = ValidationReport.Combine(ValidationReport.Empty, ValidationReport.Empty);

        await Assert.That(combined.Count).IsEqualTo(0);
        await Assert.That(combined.IsValid).IsTrue();
    }

    [Test]
    public async Task Enumerator_Should_Yield_All_Checks_In_Insertion_Order()
    {
        var first = new ValidationCheck("first", ValidationSeverity.Warning, "1");
        var second = new ValidationCheck("second", ValidationSeverity.Error, "2");
        var report = new ValidationReport([first, second]);

        var checks = report.ToList();

        await Assert.That(checks.Count).IsEqualTo(2);
        await Assert.That(checks[0]).IsEqualTo(first);
        await Assert.That(checks[1]).IsEqualTo(second);
    }
}
