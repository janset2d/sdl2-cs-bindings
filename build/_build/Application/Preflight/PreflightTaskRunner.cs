using Build.Context;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Preflight;
using Build.Domain.Preflight.Results;
using Build.Domain.Results;
using Build.Infrastructure.Vcpkg;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Application.Preflight;

public sealed class PreflightTaskRunner(
    ManifestConfig manifestConfig,
    PackageBuildConfiguration packageBuildConfiguration,
    IVcpkgManifestReader vcpkgManifestReader,
    IVersionConsistencyValidator versionConsistencyValidator,
    IStrategyCoherenceValidator strategyCoherenceValidator,
    ICoreLibraryIdentityValidator coreLibraryIdentityValidator,
    IUpstreamVersionAlignmentValidator upstreamVersionAlignmentValidator,
    ICsprojPackContractValidator csprojPackContractValidator,
    PreflightReporter preflightReporter)
{
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly PackageBuildConfiguration _packageBuildConfiguration = packageBuildConfiguration ?? throw new ArgumentNullException(nameof(packageBuildConfiguration));
    private readonly IVcpkgManifestReader _vcpkgManifestReader = vcpkgManifestReader ?? throw new ArgumentNullException(nameof(vcpkgManifestReader));
    private readonly IVersionConsistencyValidator _versionConsistencyValidator = versionConsistencyValidator ?? throw new ArgumentNullException(nameof(versionConsistencyValidator));
    private readonly IStrategyCoherenceValidator _strategyCoherenceValidator = strategyCoherenceValidator ?? throw new ArgumentNullException(nameof(strategyCoherenceValidator));
    private readonly ICoreLibraryIdentityValidator _coreLibraryIdentityValidator = coreLibraryIdentityValidator ?? throw new ArgumentNullException(nameof(coreLibraryIdentityValidator));
    private readonly IUpstreamVersionAlignmentValidator _upstreamVersionAlignmentValidator = upstreamVersionAlignmentValidator ?? throw new ArgumentNullException(nameof(upstreamVersionAlignmentValidator));
    private readonly ICsprojPackContractValidator _csprojPackContractValidator = csprojPackContractValidator ?? throw new ArgumentNullException(nameof(csprojPackContractValidator));
    private readonly PreflightReporter _preflightReporter = preflightReporter ?? throw new ArgumentNullException(nameof(preflightReporter));

    public void Run(BuildContext context)
    {
        Run(context, _packageBuildConfiguration);
    }

    public void Run(BuildContext context, PackageBuildConfiguration packageBuildConfiguration)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(packageBuildConfiguration);

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

        var upstreamVersionAlignmentValidation = _upstreamVersionAlignmentValidator.Validate(_manifestConfig, packageBuildConfiguration.ExplicitVersions);
        _preflightReporter.ReportUpstreamVersionAlignment(upstreamVersionAlignmentValidation.Validation);
        upstreamVersionAlignmentValidation.OnError(error => ThrowPreflightFailure(context.Log, "Upstream version alignment", error));

        var csprojPackContractValidation = _csprojPackContractValidator.Validate(_manifestConfig, context.Paths.RepoRoot);
        _preflightReporter.ReportCsprojPackContract(csprojPackContractValidation.Validation);
        csprojPackContractValidation.OnError(error => ThrowPreflightFailure(context.Log, "Csproj pack contract", error));
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