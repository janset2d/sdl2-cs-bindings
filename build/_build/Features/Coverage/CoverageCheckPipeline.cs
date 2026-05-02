using System.Globalization;
using Build.Host.Paths;
using Build.Integrations.Coverage;
using Build.Shared.Coverage;
using Build.Shared.Results;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Features.Coverage;

public sealed class CoverageCheckPipeline(
    ICoberturaReader coberturaReader,
    ICoverageBaselineReader coverageBaselineReader,
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    ICakeArguments arguments)
{
    internal const string CoverageFileArgument = "coverage-file";
    internal const string DefaultCoverageRelativePath = "artifacts/test-results/build-tests/coverage.cobertura.xml";

    private readonly ICoberturaReader _coberturaReader = coberturaReader ?? throw new ArgumentNullException(nameof(coberturaReader));
    private readonly ICoverageBaselineReader _coverageBaselineReader = coverageBaselineReader ?? throw new ArgumentNullException(nameof(coverageBaselineReader));
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ICakeArguments _arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));

    public void Run()
    {
        _log.Information("🔍 Running coverage ratchet check...");

        var coveragePath = ResolveCoveragePath();
        var baselinePath = _pathService.GetCoverageBaselineFile();

        _log.Information("Coverage file: {0}", coveragePath.FullPath);
        _log.Information("Baseline file: {0}", baselinePath.FullPath);

        if (!_cakeContext.FileExists(coveragePath))
        {
            throw new InvalidOperationException(
                $"❌ Coverage file not found: {coveragePath.FullPath}. " +
                $"Run 'dotnet test -- --coverage --coverage-output-format cobertura --coverage-output {DefaultCoverageRelativePath}' first, " +
                "or pass --coverage-file=<path> to point at an existing cobertura report.");
        }

        if (!_cakeContext.FileExists(baselinePath))
        {
            throw new InvalidOperationException(
                $"❌ Coverage baseline file not found: {baselinePath.FullPath}. " +
                "Create build/coverage-baseline.json with 'line_coverage_min' and 'branch_coverage_min' fields.");
        }

        var metrics = _coberturaReader.ParseFile(coveragePath);
        var baseline = _coverageBaselineReader.ParseFile(baselinePath);
        var result = CoverageThresholdValidator.Validate(metrics, baseline);

        result.OnError(error => LogFailureAndThrow(error, _log));

        LogSuccessReport(_log, result.CheckSuccess);
    }

    private FilePath ResolveCoveragePath()
    {
        var overridePath = TryGetOverridePath(_arguments);

        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var filePath = new FilePath(overridePath);
            return filePath.IsRelative
                ? _pathService.RepoRoot.CombineWithFilePath(filePath)
                : filePath;
        }

        return _pathService.RepoRoot.CombineWithFilePath(DefaultCoverageRelativePath);
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
