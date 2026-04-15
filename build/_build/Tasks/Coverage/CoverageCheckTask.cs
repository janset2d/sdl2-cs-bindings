/*
 * SPDX-FileCopyrightText: 2025 James Williamson <james@semick.dev>
 * SPDX-License-Identifier: MIT
 */

using System.Globalization;
using Build.Context;
using Build.Modules.Contracts;
using Build.Modules.Coverage;
using Build.Modules.Coverage.Models;
using Build.Modules.Coverage.Results;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks.Coverage;

/// <summary>
/// Static-floor coverage ratchet: compares the cobertura report against a baseline file
/// committed in the repo. Fails the build if line or branch coverage drops below the floor.
/// </summary>
/// <remarks>
/// <para>The coverage report is expected to come from
/// <c>dotnet test -- --coverage --coverage-output-format cobertura</c>.</para>
/// <para>Default coverage file location: <c>artifacts/test-results/build-tests/coverage.cobertura.xml</c>.
/// Override with <c>--coverage-file=&lt;path&gt;</c>. Baseline always lives at
/// <c>build/coverage-baseline.json</c>.</para>
/// </remarks>
[TaskName("Coverage-Check")]
[TaskDescription("Validates test coverage against the baseline floor in build/coverage-baseline.json (ratchet policy)")]
public sealed class CoverageCheckTask(ICoberturaReader coberturaReader, ICoverageBaselineReader coverageBaselineReader) : FrostingTask<BuildContext>
{
    internal const string CoverageFileArgument = "coverage-file";
    internal const string DefaultCoverageRelativePath = "artifacts/test-results/build-tests/coverage.cobertura.xml";

    private readonly ICoberturaReader _coberturaReader = coberturaReader ?? throw new ArgumentNullException(nameof(coberturaReader));
    private readonly ICoverageBaselineReader _coverageBaselineReader = coverageBaselineReader ?? throw new ArgumentNullException(nameof(coverageBaselineReader));

    public override void Run(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Log.Information("🔍 Running coverage ratchet check...");

        var coveragePath = ResolveCoveragePath(context);
        var baselinePath = context.Paths.GetCoverageBaselineFile();

        context.Log.Information("Coverage file: {0}", coveragePath.FullPath);
        context.Log.Information("Baseline file: {0}", baselinePath.FullPath);

        if (!context.FileExists(coveragePath))
        {
            throw new InvalidOperationException(
                $"❌ Coverage file not found: {coveragePath.FullPath}. " +
                $"Run 'dotnet test -- --coverage --coverage-output-format cobertura --coverage-output {DefaultCoverageRelativePath}' first, " +
                "or pass --coverage-file=<path> to point at an existing cobertura report.");
        }

        if (!context.FileExists(baselinePath))
        {
            throw new InvalidOperationException(
                $"❌ Coverage baseline file not found: {baselinePath.FullPath}. " +
                "Create build/coverage-baseline.json with 'line_coverage_min' and 'branch_coverage_min' fields.");
        }

        var metrics = _coberturaReader.ParseFile(coveragePath);
        var baseline = _coverageBaselineReader.ParseFile(baselinePath);
        var result = CoverageThresholdValidator.Validate(metrics, baseline);

        result.ThrowIfError(error => LogFailureAndThrow(error, context.Log));

        LogSuccessReport(context.Log, result.CheckSuccess);
    }

    internal static FilePath ResolveCoveragePath(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var overridePath = TryGetOverridePath(context.Arguments);

        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var filePath = new FilePath(overridePath);
            return filePath.IsRelative
                ? context.Paths.RepoRoot.CombineWithFilePath(filePath)
                : filePath;
        }

        return context.Paths.RepoRoot.CombineWithFilePath(DefaultCoverageRelativePath);
    }

    private static string? TryGetOverridePath(ICakeArguments arguments)
    {
        if (!arguments.HasArgument(CoverageFileArgument))
        {
            return null;
        }

        var values = arguments.GetArguments(CoverageFileArgument);
        return values.Count == 0 ? null : values.First();
    }

    private static void LogSuccessReport(ICakeLog log, CoverageCheckSuccess success)
    {
        LogMetrics(log, success.Metrics, success.Baseline);
        log.Information("");
        log.Information("✅ Coverage ratchet check PASSED");
    }

    private static void LogFailureAndThrow(CoverageError error, ICakeLog log)
    {
        if (error is CoverageThresholdViolation violation)
        {
            LogMetrics(log, violation.Metrics, violation.Baseline);
            log.Information("");

            foreach (var failure in violation.Failures)
            {
                log.Error("❌ {0}", failure);
            }

            log.Error("   To raise the floor, update build/coverage-baseline.json after a confirmed coverage increase.");
        }
        else
        {
            log.Error("❌ {0}", error.Message);
        }

        throw new CakeException("Coverage ratchet check failed — coverage dropped below the configured floor.");
    }

    private static void LogMetrics(ICakeLog log, CoverageMetrics metrics, CoverageBaseline baseline)
    {
        log.Information("");
        log.Information("📊 Coverage report:");
        log.Information(string.Create(CultureInfo.InvariantCulture,
            $"  Line:   {metrics.LinePercent,6:F2}% ({metrics.LinesCovered}/{metrics.LinesValid})    floor: {baseline.LineCoverageMin:F2}%"));
        log.Information(string.Create(CultureInfo.InvariantCulture,
            $"  Branch: {metrics.BranchPercent,6:F2}% ({metrics.BranchesCovered}/{metrics.BranchesValid})    floor: {baseline.BranchCoverageMin:F2}%"));
    }
}
