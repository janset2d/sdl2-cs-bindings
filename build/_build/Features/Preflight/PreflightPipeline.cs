using Build.Host;
using Build.Integrations.Vcpkg;
using Build.Shared.Manifest;
using Build.Shared.Packaging;
using Build.Shared.Results;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Features.Preflight;

public sealed class PreflightPipeline(
    ManifestConfig manifestConfig,
    IVcpkgManifestReader vcpkgManifestReader,
    IVersionConsistencyValidator versionConsistencyValidator,
    IStrategyCoherenceValidator strategyCoherenceValidator,
    ICoreLibraryIdentityValidator coreLibraryIdentityValidator,
    IUpstreamVersionAlignmentValidator upstreamVersionAlignmentValidator,
    ICsprojPackContractValidator csprojPackContractValidator,
    IG58CrossFamilyDepResolvabilityValidator g58CrossFamilyDepResolvabilityValidator,
    PreflightReporter preflightReporter)
{
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IVcpkgManifestReader _vcpkgManifestReader = vcpkgManifestReader ?? throw new ArgumentNullException(nameof(vcpkgManifestReader));
    private readonly IVersionConsistencyValidator _versionConsistencyValidator = versionConsistencyValidator ?? throw new ArgumentNullException(nameof(versionConsistencyValidator));
    private readonly IStrategyCoherenceValidator _strategyCoherenceValidator = strategyCoherenceValidator ?? throw new ArgumentNullException(nameof(strategyCoherenceValidator));
    private readonly ICoreLibraryIdentityValidator _coreLibraryIdentityValidator = coreLibraryIdentityValidator ?? throw new ArgumentNullException(nameof(coreLibraryIdentityValidator));
    private readonly IUpstreamVersionAlignmentValidator _upstreamVersionAlignmentValidator = upstreamVersionAlignmentValidator ?? throw new ArgumentNullException(nameof(upstreamVersionAlignmentValidator));
    private readonly ICsprojPackContractValidator _csprojPackContractValidator = csprojPackContractValidator ?? throw new ArgumentNullException(nameof(csprojPackContractValidator));
    private readonly IG58CrossFamilyDepResolvabilityValidator _g58CrossFamilyDepResolvabilityValidator = g58CrossFamilyDepResolvabilityValidator ?? throw new ArgumentNullException(nameof(g58CrossFamilyDepResolvabilityValidator));
    private readonly PreflightReporter _preflightReporter = preflightReporter ?? throw new ArgumentNullException(nameof(preflightReporter));

    /// <summary>
    /// C.2 uniform stage-runner contract: <c>Task RunAsync(BuildContext, TRequest, CT)</c>.
    /// PreFlight validators are synchronous — the body stays sync and the method returns
    /// <see cref="Task.CompletedTask"/>. Wrapping in an async signature keeps the 7-runner
    /// surface uniform without forcing fake-async propagation into the validators themselves.
    /// </summary>
    public Task RunAsync(BuildContext context, PreflightRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Run(context, request.Versions);
        return Task.CompletedTask;
    }

    private void Run(BuildContext context, IReadOnlyDictionary<string, NuGetVersion> versions)
    {
        var manifestPath = context.Paths.GetManifestFile();
        var vcpkgManifestPath = context.Paths.GetVcpkgManifestFile();
        EnsurePreflightInputsReady(context, manifestPath, vcpkgManifestPath);

        _preflightReporter.ReportRunStart();

        var vcpkgManifest = _vcpkgManifestReader.ParseFile(vcpkgManifestPath);

        var versionConsistencyValidation = _versionConsistencyValidator.Validate(_manifestConfig, vcpkgManifest, manifestPath, vcpkgManifestPath);
        _preflightReporter.ReportVersionConsistency(versionConsistencyValidation.Validation);
        versionConsistencyValidation.OnError(error => ThrowPreflightFailure(context.Log, "Version consistency", error));

        var strategyCoherenceValidation = _strategyCoherenceValidator.Validate(_manifestConfig.Runtimes);
        _preflightReporter.ReportStrategyCoherence(strategyCoherenceValidation.Validation);
        strategyCoherenceValidation.OnError(error => ThrowPreflightFailure(context.Log, "Strategy coherence", error));

        var coreLibraryIdentityValidation = _coreLibraryIdentityValidator.Validate(_manifestConfig);
        _preflightReporter.ReportCoreLibraryIdentity(coreLibraryIdentityValidation.Validation);
        coreLibraryIdentityValidation.OnError(error => ThrowPreflightFailure(context.Log, "Core library identity", error));

        var upstreamVersionAlignmentValidation = _upstreamVersionAlignmentValidator.Validate(_manifestConfig, versions);
        _preflightReporter.ReportUpstreamVersionAlignment(upstreamVersionAlignmentValidation.Validation);
        upstreamVersionAlignmentValidation.OnError(error => ThrowPreflightFailure(context.Log, "Upstream version alignment", error));

        var csprojPackContractValidation = _csprojPackContractValidator.Validate(_manifestConfig, context.Paths.RepoRoot);
        _preflightReporter.ReportCsprojPackContract(csprojPackContractValidation.Validation);
        csprojPackContractValidation.OnError(error => ThrowPreflightFailure(context.Log, "Csproj pack contract", error));

        // G58 scope-contains mirror (Deniz Q2 2026-04-21 decision): PreFlight runs the same
        // check Pack runs, strictly scope-contains (no feed probe). Catches satellite-only
        // --explicit-version misuse before Harvest/vcpkg spins up minutes of work.
        var g58Validation = _g58CrossFamilyDepResolvabilityValidator.Validate(versions, _manifestConfig);
        _preflightReporter.ReportG58CrossFamilyResolvability(g58Validation);
        if (g58Validation.HasErrors)
        {
            throw new CakeException(
                "Pre-flight check failed during G58 cross-family dependency resolvability validation. " +
                $"{g58Validation.Checks.Count(check => check.IsError)} error(s). Use --verbosity=diagnostic for details.");
        }
    }

    private static void EnsurePreflightInputsReady(BuildContext context, FilePath manifestPath, FilePath vcpkgManifestPath)
    {
        if (!context.FileExists(manifestPath))
        {
            throw new CakeException(
                $"PreFlightCheck precondition failed: manifest file '{manifestPath.FullPath}' is missing. " +
                "Run from the repository root or pass --repo-root to point Cake at a valid checkout.");
        }

        if (!context.FileExists(vcpkgManifestPath))
        {
            throw new CakeException(
                $"PreFlightCheck precondition failed: vcpkg manifest '{vcpkgManifestPath.FullPath}' is missing. " +
                "Run from the repository root or pass --repo-root to point Cake at a valid checkout.");
        }
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
