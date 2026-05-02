using Build.Host.Paths;
using Build.Integrations.Vcpkg;
using Build.Shared.Manifest;
using Build.Shared.Packaging;
using Build.Shared.Results;
using Build.Shared.Versioning;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Features.Preflight;

public sealed class PreflightPipeline(
    ManifestConfig manifestConfig,
    IVcpkgManifestReader vcpkgManifestReader,
    StrategyCoherenceValidator strategyCoherenceValidator,
    IUpstreamVersionAlignmentValidator upstreamVersionAlignmentValidator,
    ICsprojPackContractValidator csprojPackContractValidator,
    IG58CrossFamilyDepResolvabilityValidator g58CrossFamilyDepResolvabilityValidator,
    PreflightReporter preflightReporter,
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService)
{
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IVcpkgManifestReader _vcpkgManifestReader = vcpkgManifestReader ?? throw new ArgumentNullException(nameof(vcpkgManifestReader));
    private readonly StrategyCoherenceValidator _strategyCoherenceValidator = strategyCoherenceValidator ?? throw new ArgumentNullException(nameof(strategyCoherenceValidator));
    private readonly IUpstreamVersionAlignmentValidator _upstreamVersionAlignmentValidator = upstreamVersionAlignmentValidator ?? throw new ArgumentNullException(nameof(upstreamVersionAlignmentValidator));
    private readonly ICsprojPackContractValidator _csprojPackContractValidator = csprojPackContractValidator ?? throw new ArgumentNullException(nameof(csprojPackContractValidator));
    private readonly IG58CrossFamilyDepResolvabilityValidator _g58CrossFamilyDepResolvabilityValidator = g58CrossFamilyDepResolvabilityValidator ?? throw new ArgumentNullException(nameof(g58CrossFamilyDepResolvabilityValidator));
    private readonly PreflightReporter _preflightReporter = preflightReporter ?? throw new ArgumentNullException(nameof(preflightReporter));
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    public Task RunAsync(PreflightRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Run(request.Versions);
        return Task.CompletedTask;
    }

    private void Run(IReadOnlyDictionary<string, NuGetVersion> versions)
    {
        var manifestPath = _pathService.GetManifestFile();
        var vcpkgManifestPath = _pathService.GetVcpkgManifestFile();
        EnsurePreflightInputsReady(manifestPath, vcpkgManifestPath);

        _preflightReporter.ReportRunStart();

        var vcpkgManifest = _vcpkgManifestReader.ParseFile(vcpkgManifestPath);

        var versionConsistencyValidation = VersionConsistencyValidator.Validate(_manifestConfig, vcpkgManifest, manifestPath, vcpkgManifestPath);
        _preflightReporter.ReportVersionConsistency(versionConsistencyValidation.Validation);
        versionConsistencyValidation.OnError(error => ThrowPreflightFailure(_log, "Version consistency", error));

        var strategyCoherenceValidation = _strategyCoherenceValidator.Validate(_manifestConfig.Runtimes);
        _preflightReporter.ReportStrategyCoherence(strategyCoherenceValidation.Validation);
        strategyCoherenceValidation.OnError(error => ThrowPreflightFailure(_log, "Strategy coherence", error));

        var coreLibraryIdentityValidation = CoreLibraryIdentityValidator.Validate(_manifestConfig);
        _preflightReporter.ReportCoreLibraryIdentity(coreLibraryIdentityValidation.Validation);
        coreLibraryIdentityValidation.OnError(error => ThrowPreflightFailure(_log, "Core library identity", error));

        var upstreamVersionAlignmentValidation = _upstreamVersionAlignmentValidator.Validate(_manifestConfig, versions);
        _preflightReporter.ReportUpstreamVersionAlignment(upstreamVersionAlignmentValidation.Validation);
        upstreamVersionAlignmentValidation.OnError(error => ThrowPreflightFailure(_log, "Upstream version alignment", error));

        var csprojPackContractValidation = _csprojPackContractValidator.Validate(_manifestConfig, _pathService.RepoRoot);
        _preflightReporter.ReportCsprojPackContract(csprojPackContractValidation.Validation);
        csprojPackContractValidation.OnError(error => ThrowPreflightFailure(_log, "Csproj pack contract", error));

        var g58Validation = _g58CrossFamilyDepResolvabilityValidator.Validate(versions, _manifestConfig);
        _preflightReporter.ReportG58CrossFamilyResolvability(g58Validation);
        if (g58Validation.HasErrors)
        {
            throw new CakeException(
                "Pre-flight check failed during G58 cross-family dependency resolvability validation. " +
                $"{g58Validation.Checks.Count(check => check.IsError)} error(s). Use --verbosity=diagnostic for details.");
        }
    }

    private void EnsurePreflightInputsReady(FilePath manifestPath, FilePath vcpkgManifestPath)
    {
        if (!_cakeContext.FileExists(manifestPath))
        {
            throw new CakeException(
                $"PreFlightCheck precondition failed: manifest file '{manifestPath.FullPath}' is missing. " +
                "Run from the repository root or pass --repo-root to point Cake at a valid checkout.");
        }

        if (!_cakeContext.FileExists(vcpkgManifestPath))
        {
            throw new CakeException(
                $"PreFlightCheck precondition failed: vcpkg manifest '{vcpkgManifestPath.FullPath}' is missing. " +
                "Run from the repository root or pass --repo-root to point Cake at a valid checkout.");
        }
    }

    private static void ThrowPreflightFailure(ICakeLog log, string phase, BuildError error)
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
