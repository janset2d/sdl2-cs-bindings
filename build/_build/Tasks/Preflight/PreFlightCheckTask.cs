/*
 * SPDX-FileCopyrightText: 2025 James Williamson <james@semick.dev>
 * SPDX-License-Identifier: MIT
 */

using Build.Context;
using Build.Context.Models;
using Build.Modules.Contracts;
using Build.Modules.Preflight.Results;
using Build.Modules.Results;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks.Preflight;

/// <summary>
/// Pre-flight validation task that checks version consistency between manifest.json and vcpkg.json,
/// and validates strategy coherence for runtime entries in manifest.json.
/// This task ensures that the intended native library versions in manifest.json match
/// the actual vcpkg overrides before starting any build operations.
/// </summary>
[TaskName("PreFlightCheck")]
[TaskDescription("Validates manifest-vcpkg version consistency and runtime strategy coherence (partial gate)")]
public sealed class PreFlightCheckTask : FrostingTask<BuildContext>
{
    private readonly ManifestConfig _manifestConfig;
    private readonly IVcpkgManifestReader _vcpkgManifestReader;
    private readonly IVersionConsistencyValidator _versionConsistencyValidator;
    private readonly IStrategyCoherenceValidator _strategyCoherenceValidator;
    private readonly IPreflightReporter _preflightReporter;

    public PreFlightCheckTask(
        ManifestConfig manifestConfig,
        IVcpkgManifestReader vcpkgManifestReader,
        IVersionConsistencyValidator versionConsistencyValidator,
        IStrategyCoherenceValidator strategyCoherenceValidator,
        IPreflightReporter preflightReporter)
    {
        _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
        _vcpkgManifestReader = vcpkgManifestReader ?? throw new ArgumentNullException(nameof(vcpkgManifestReader));
        _versionConsistencyValidator = versionConsistencyValidator ?? throw new ArgumentNullException(nameof(versionConsistencyValidator));
        _strategyCoherenceValidator = strategyCoherenceValidator ?? throw new ArgumentNullException(nameof(strategyCoherenceValidator));
        _preflightReporter = preflightReporter ?? throw new ArgumentNullException(nameof(preflightReporter));
    }

    public override void Run(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _preflightReporter.ReportRunStart();

        var manifestPath = context.Paths.GetManifestFile();
        var vcpkgManifestPath = context.Paths.GetVcpkgManifestFile();
        var vcpkgManifest = _vcpkgManifestReader.ParseFile(vcpkgManifestPath);

        var versionConsistencyValidation = _versionConsistencyValidator.Validate(_manifestConfig, vcpkgManifest, manifestPath, vcpkgManifestPath);
        _preflightReporter.ReportVersionConsistency(versionConsistencyValidation.Validation);

        versionConsistencyValidation.OnError(error => ThrowPreflightFailure(context.Log, "Version consistency", error));

        var strategyCoherenceValidation = _strategyCoherenceValidator.Validate(_manifestConfig.Runtimes);
        _preflightReporter.ReportStrategyCoherence(strategyCoherenceValidation.Validation);

        strategyCoherenceValidation.OnError(error => ThrowPreflightFailure(context.Log, "Strategy coherence", error));
    }

    private static void ThrowPreflightFailure(ICakeLog log, string phase, PreflightError error)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        ArgumentNullException.ThrowIfNull(error);

        log.Error("{0} validation failed: {1}", phase, error.Message);

        if (error.Exception is not null)
        {
            log.Verbose("Details: {0}", error.Exception);
        }

        throw new CakeException($"Pre-flight check failed during {phase.ToLowerInvariant()} validation. Use --verbosity=diagnostic for details. Error: {error.Message}");
    }
}
