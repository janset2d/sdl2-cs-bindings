using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Build.Data.Harvest;
using Build.Host;
using Build.Data.Manifest;
using Build.Targets.Harvest.Reporting;
using Build.Targets.Harvest.Services;
using Build.Validation.Harvesting;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Targets.Harvest;

/// <summary>
/// Cake target that produces the per-RID native payload + license attribution from the
/// vcpkg-installed binary closure. Owns top-level orchestration directly: validates target
/// inputs, runs cohort preconditions, resolves the library set, and threads each library
/// through walker → leak validator → planner → deployer → status repo with reporter-mediated
/// console + log surface. Direct successor to the retired pre-migration HarvestPipeline.
/// </summary>
[TaskName("Harvest")]
[TaskDescription("Harvests vcpkg-installed binaries + transitive deps into per-RID native payload and license attribution")]
public sealed class HarvestTask(
    IHarvestPreconditionsValidator preconditions,
    IBinaryClosureWalker walker,
    IHybridStaticLeakValidator leakValidator,
    IArtifactPlanner planner,
    IArtifactDeployer deployer,
    IHarvestStatusRepository statusRepo,
    HarvestReporter reporter,
    ManifestConfig manifestConfig) : AsyncFrostingTask<BuildContext>
{
    private readonly IHarvestPreconditionsValidator _preconditions = preconditions ?? throw new ArgumentNullException(nameof(preconditions));
    private readonly IBinaryClosureWalker _walker = walker ?? throw new ArgumentNullException(nameof(walker));
    private readonly IHybridStaticLeakValidator _leakValidator = leakValidator ?? throw new ArgumentNullException(nameof(leakValidator));
    private readonly IArtifactPlanner _planner = planner ?? throw new ArgumentNullException(nameof(planner));
    private readonly IArtifactDeployer _deployer = deployer ?? throw new ArgumentNullException(nameof(deployer));
    private readonly IHarvestStatusRepository _statusRepo = statusRepo ?? throw new ArgumentNullException(nameof(statusRepo));
    private readonly HarvestReporter _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.Runtime.Rid))
        {
            throw new CakeException(
                "Harvest requires --rid <rid>. Example: --target Harvest --rid win-x64");
        }

        var preReport = _preconditions.Validate();
        if (!preReport.IsValid)
        {
            foreach (var error in preReport.Errors)
            {
                context.Log.Error(error.Message);
            }

            throw new CakeException(preReport.Errors[0].Message);
        }

        var outputBase = context.Paths.HarvestOutput;
        context.EnsureDirectoryExists(outputBase);

        var libraries = ResolveLibrariesToHarvest(context.Libraries);
        if (libraries.Count == 0)
        {
            context.Log.Warning("No libraries found to harvest (either specified or in manifest).");
            return;
        }

        _reporter.LogStarting(libraries.Select(l => l.Name).ToArray());

        foreach (var library in libraries)
        {
            await ProcessLibraryAsync(library, outputBase).ConfigureAwait(false);
        }

        _reporter.LogCompleted();
    }

    [SuppressMessage("Design", "MA0051", Justification = "Linear orchestration of fail-fast harvest steps; per-step collaborators carry the algorithmic weight per ADR-002 §5. Inlining keeps the build story readable rather than hiding it behind ceremonial Process/Handle/Do helpers per checklist §4.6.")]
    private async Task ProcessLibraryAsync(LibraryManifest library, DirectoryPath outputBase)
    {
        _reporter.StartLibrary(library.Name);

        try
        {
            _statusRepo.Invalidate(library.Name);

            var closureResult = await _walker.BuildClosureAsync(library).ConfigureAwait(false);
            if (closureResult.IsFailure)
            {
                throw await CreateLibraryFailureAsync(library.Name, "Binary closure", closureResult.Error.Message, closureResult.Error.Exception).ConfigureAwait(false);
            }
            var closure = closureResult.Value;

            var leakReport = _leakValidator.Validate(closure, library);
            if (!leakReport.IsValid)
            {
                _reporter.ReportLeakReport(library.Name, leakReport);
                throw await CreateLibraryFailureAsync(
                    library.Name,
                    "Hybrid-static leak validation",
                    $"{leakReport.Errors.Count} violation(s) detected.",
                    exception: null).ConfigureAwait(false);
            }
            if (leakReport.HasWarnings)
            {
                _reporter.ReportLeakReport(library.Name, leakReport);
            }

            var plannerResult = await _planner.CreatePlanAsync(library, closure, outputBase).ConfigureAwait(false);
            if (plannerResult.IsFailure)
            {
                throw await CreateLibraryFailureAsync(library.Name, "Artifact planning", plannerResult.Error.Message, plannerResult.Error.Exception).ConfigureAwait(false);
            }
            var deploymentPlan = plannerResult.Value;

            var deployerResult = await _deployer.DeployArtifactsAsync(deploymentPlan).ConfigureAwait(false);
            if (deployerResult.IsFailure)
            {
                throw await CreateLibraryFailureAsync(library.Name, "Artifact copying", deployerResult.Error.Message, deployerResult.Error.Exception).ConfigureAwait(false);
            }
            var statistics = deployerResult.Value;

            // [G1] post-harvest invariant: a successful harvest must have produced at least one
            // primary binary. Defence-in-depth for the case where walker + planner + deployer all
            // returned success but the resolved primary set ended up empty (silent feature-flag
            // degradation, partial vcpkg install). BinaryClosureWalker is the primary guard; this
            // post-check ensures no downstream consumer ingests a rid-status=true with zero primaries.
            if (statistics.PrimaryFiles.Count == 0)
            {
                throw await CreateLibraryFailureAsync(
                    library.Name,
                    "Harvest",
                    $"Harvest produced zero primary binaries for '{library.Name}'. Closure walker and planner returned success but no primary files were deployed. Inspect vcpkg install output and manifest primary_binaries patterns.",
                    exception: null).ConfigureAwait(false);
            }

            await _statusRepo.WriteSuccessAsync(library.Name, statistics).ConfigureAwait(false);
            _reporter.FinishLibrary(library.Name, statistics);
        }
        catch (OperationCanceledException)
        {
            _reporter.CancelLibrary(library.Name);
            throw;
        }
        catch (CakeException)
        {
            // CreateLibraryFailureAsync already reported + persisted error rid-status; re-throw to surface to the Cake host.
            throw;
        }
        catch (Exception ex) when (IsOperationalHarvestException(ex))
        {
            throw await CreateLibraryFailureAsync(
                library.Name,
                "Harvest",
                $"Unexpected operational error during harvest of {library.Name}: {ex.Message}",
                ex).ConfigureAwait(false);
        }
    }

    private async Task<CakeException> CreateLibraryFailureAsync(string libraryName, string phase, string message, Exception? exception)
    {
        _reporter.ReportPhaseFailure(libraryName, phase, message, exception);
        await _statusRepo.WriteErrorAsync(libraryName, message).ConfigureAwait(false);
        return new CakeException($"{phase} failed for '{libraryName}'.");
    }

    private List<LibraryManifest> ResolveLibrariesToHarvest(IReadOnlyList<string> requestedLibraries)
    {
        var allManifestLibraries = _manifestConfig.LibraryManifests.ToList();

        if (requestedLibraries.Count == 0)
        {
            return allManifestLibraries;
        }

        var librariesToHarvest = new List<LibraryManifest>(requestedLibraries.Count);
        foreach (var specLibName in requestedLibraries)
        {
            var manifest = allManifestLibraries.SingleOrDefault(m => string.Equals(m.Name, specLibName, StringComparison.OrdinalIgnoreCase))
                ?? throw new CakeException($"Specified library '{specLibName}' for harvest not found in manifest.");

            librariesToHarvest.Add(manifest);
        }

        return librariesToHarvest;
    }

    private static bool IsOperationalHarvestException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or JsonException;
}
