using System.Text.Json;
using Build.Host.Cake;
using Build.Host.Paths;
using Build.Shared.Harvesting;
using Build.Shared.Manifest;
using Build.Shared.Results;
using Build.Shared.Runtime;
using Build.Shared.Strategy;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Spectre.Console;
using IoPath = System.IO.Path;

namespace Build.Features.Harvesting;

public sealed class HarvestPipeline(
    IBinaryClosureWalker binaryClosureWalker,
    IArtifactPlanner artifactPlanner,
    IArtifactDeployer artifactDeployer,
    IDependencyPolicyValidator dependencyPolicyValidator,
    IRuntimeProfile runtimeProfile,
    ManifestConfig manifestConfig,
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService)
{
    private readonly IBinaryClosureWalker _binaryClosureWalker = binaryClosureWalker ?? throw new ArgumentNullException(nameof(binaryClosureWalker));
    private readonly IArtifactPlanner _artifactPlanner = artifactPlanner ?? throw new ArgumentNullException(nameof(artifactPlanner));
    private readonly IArtifactDeployer _artifactDeployer = artifactDeployer ?? throw new ArgumentNullException(nameof(artifactDeployer));
    private readonly IDependencyPolicyValidator _dependencyPolicyValidator = dependencyPolicyValidator ?? throw new ArgumentNullException(nameof(dependencyPolicyValidator));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    private static JsonSerializerOptions JsonOptions => HarvestJsonContract.Options;

    public async Task RunAsync(HarvestRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureHarvestInputsReady();

        var outputBase = _pathService.HarvestOutput;
        _cakeContext.EnsureDirectoryExists(outputBase);

        var librariesToHarvest = ResolveLibrariesToHarvest(request.Libraries);

        if (librariesToHarvest.Count == 0)
        {
            _log.Warning("No libraries found to harvest (either specified or in manifest).");
            return;
        }

        foreach (var manifest in librariesToHarvest)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessLibraryAsync(manifest, outputBase);
        }

        AnsiConsole.Write(new Rule("[green]Harvest completed successfully[/]").RuleStyle("grey"));
    }

    private void EnsureHarvestInputsReady()
    {
        var tripletDir = _pathService.GetVcpkgInstalledTripletDir(_runtimeProfile.Triplet);
        if (!_cakeContext.DirectoryExists(tripletDir))
        {
            throw new CakeException(
                $"Harvest precondition failed: vcpkg triplet directory '{tripletDir.FullPath}' is missing for triplet '{_runtimeProfile.Triplet}'. " +
                $"Run '--target EnsureVcpkgDependencies --rid {_runtimeProfile.Rid}' first.");
        }
    }

    private List<LibraryManifest> ResolveLibrariesToHarvest(IReadOnlyList<string> requestedLibraries)
    {

        ArgumentNullException.ThrowIfNull(requestedLibraries);

        var allManifestLibraries = _manifestConfig.LibraryManifests.ToList();

        if (requestedLibraries.Count == 0)
        {
            _log.Information("No specific libraries specified for harvest. Processing all libraries from manifest.");
            return allManifestLibraries;
        }

        _log.Information("Processing specified libraries for harvest: {0}", string.Join(", ", requestedLibraries));

        var librariesToHarvest = new List<LibraryManifest>(requestedLibraries.Count);
        foreach (var specLibName in requestedLibraries)
        {
            var manifest = allManifestLibraries.SingleOrDefault(m => string.Equals(m.Name, specLibName, StringComparison.OrdinalIgnoreCase))
                ?? throw new CakeException($"Specified library '{specLibName}' for harvest not found in manifest.");

            librariesToHarvest.Add(manifest);
        }

        return librariesToHarvest;
    }

    private async Task ProcessLibraryAsync(LibraryManifest manifest, DirectoryPath outputBase)
    {

        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(outputBase);

        AnsiConsole.Write(new Rule($"[yellow]Harvest: {manifest.Name}[/]").RuleStyle("grey"));
        PrepareLibraryOutputForCurrentRid(manifest, outputBase);

        try
        {
            var statistics = await ExecuteHarvestPipelineAsync(manifest, outputBase);
            DisplayHarvestReportSummary(statistics);
            AnsiConsole.Write(new Rule($"[green]Finished Harvest: {manifest.Name}[/]").RuleStyle("grey"));
        }
        catch (OperationCanceledException)
        {
            _log.Warning("Harvest canceled for '{0}'.", manifest.Name);
            AnsiConsole.Write(new Rule($"[yellow]Canceled Harvest: {manifest.Name}[/]").RuleStyle("grey"));
            throw;
        }
        catch (CakeException)
        {
            await HandleKnownHarvestFailureAsync(manifest, outputBase, $"Harvest failed for {manifest.Name}");
            throw;
        }
        catch (Exception ex) when (IsOperationalHarvestException(ex))
        {
            await HandleOperationalHarvestFailureAsync(manifest, outputBase, ex);
            throw;
        }
    }

    private void PrepareLibraryOutputForCurrentRid(LibraryManifest manifest, DirectoryPath outputBase)
    {

        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(outputBase);

        // outputBase remains in the signature for parity with the historical call site,
        // but all path construction now goes through IPathService so the layout is governed
        // in one place. PathService roots under _pathService.HarvestOutput � same dir.
        CleanCurrentRidPayload(manifest);
        InvalidateCrossRidReceipts(manifest);
    }

    private void CleanCurrentRidPayload(LibraryManifest manifest)
    {
        var paths = _pathService;
        var currentRidRuntimeRoot = paths.GetHarvestLibraryRidRuntimesDir(manifest.Name, _runtimeProfile.Rid);
        var currentRidStatusFile = paths.GetHarvestLibraryRidStatusFile(manifest.Name, _runtimeProfile.Rid);
        // Post-H1 (2026-04-18): license cleanup is RID-scoped to match the RID-scoped deployment
        // layout written by ArtifactPlanner. Sibling RIDs keep their license evidence intact;
        // cross-RID consolidation belongs to ConsolidateHarvestTask, not Harvest's per-RID run.
        var currentRidLicenseRoot = paths.GetHarvestLibraryRidLicensesDir(manifest.Name, _runtimeProfile.Rid);

        if (_cakeContext.DirectoryExists(currentRidRuntimeRoot))
        {
            _log.Verbose("Cleaning existing harvest payload for {0}/{1}: {2}", manifest.Name, _runtimeProfile.Rid, currentRidRuntimeRoot);
            _cakeContext.DeleteDirectory(currentRidRuntimeRoot, new DeleteDirectorySettings { Recursive = true, Force = true });
        }

        if (_cakeContext.FileExists(currentRidStatusFile))
        {
            _log.Verbose("Deleting existing harvest RID status for {0}/{1}: {2}", manifest.Name, _runtimeProfile.Rid, currentRidStatusFile);
            _cakeContext.DeleteFile(currentRidStatusFile);
        }

        if (_cakeContext.DirectoryExists(currentRidLicenseRoot))
        {
            _log.Verbose("Refreshing harvest license payload for {0}/{1}: {2}", manifest.Name, _runtimeProfile.Rid, currentRidLicenseRoot);
            _cakeContext.DeleteDirectory(currentRidLicenseRoot, new DeleteDirectorySettings { Recursive = true, Force = true });
        }
    }

    /// <summary>
    /// H1 invalidation: any Harvest re-run can change the successful-RID set OR the per-RID
    /// license evidence, so the cross-RID receipts produced by ConsolidateHarvestTask
    /// (licenses/_consolidated/, harvest-manifest.json, harvest-summary.json) are always
    /// stale after Harvest touches this library. Deleting them here turns the implicit
    /// dependency into an explicit invalidation � Package's gate will fail until
    /// ConsolidateHarvest regenerates the receipts. Skipping this would let a stale
    /// receipt authorize a pack against empty licenses/_consolidated/, silently dropping
    /// attribution.
    /// <para>
    /// Also cleans up the *.tmp/ and *.tmp.json artifacts that ConsolidateHarvestTask uses
    /// for its staged-replace swap. If Consolidate crashed mid-flight on a previous run,
    /// those orphans survive into the next cycle; cleaning them here prevents accumulated
    /// noise + guarantees Consolidate starts from a clean slate for the staged-replace
    /// pattern to work correctly.
    /// </para>
    /// </summary>
    private void InvalidateCrossRidReceipts(LibraryManifest manifest)
    {
        var paths = _pathService;

        var consolidatedLicenseRoot = paths.GetHarvestLibraryConsolidatedLicensesDir(manifest.Name);
        if (_cakeContext.DirectoryExists(consolidatedLicenseRoot))
        {
            _log.Verbose("Invalidating consolidated license payload for {0}: {1}", manifest.Name, consolidatedLicenseRoot);
            _cakeContext.DeleteDirectory(consolidatedLicenseRoot, new DeleteDirectorySettings { Recursive = true, Force = true });
        }

        var consolidatedTempRoot = paths.GetHarvestLibraryConsolidatedLicensesTempDir(manifest.Name);
        if (_cakeContext.DirectoryExists(consolidatedTempRoot))
        {
            _log.Verbose("Cleaning orphan consolidated-tmp for {0}: {1}", manifest.Name, consolidatedTempRoot);
            _cakeContext.DeleteDirectory(consolidatedTempRoot, new DeleteDirectorySettings { Recursive = true, Force = true });
        }

        var harvestManifestFile = paths.GetHarvestLibraryManifestFile(manifest.Name);
        if (_cakeContext.FileExists(harvestManifestFile))
        {
            _log.Verbose("Invalidating stale harvest manifest for {0}: {1}", manifest.Name, harvestManifestFile);
            _cakeContext.DeleteFile(harvestManifestFile);
        }

        var harvestManifestTempFile = paths.GetHarvestLibraryManifestTempFile(manifest.Name);
        if (_cakeContext.FileExists(harvestManifestTempFile))
        {
            _log.Verbose("Cleaning orphan harvest-manifest-tmp for {0}: {1}", manifest.Name, harvestManifestTempFile);
            _cakeContext.DeleteFile(harvestManifestTempFile);
        }

        var harvestSummaryFile = paths.GetHarvestLibrarySummaryFile(manifest.Name);
        if (_cakeContext.FileExists(harvestSummaryFile))
        {
            _log.Verbose("Invalidating stale harvest summary for {0}: {1}", manifest.Name, harvestSummaryFile);
            _cakeContext.DeleteFile(harvestSummaryFile);
        }

        var harvestSummaryTempFile = paths.GetHarvestLibrarySummaryTempFile(manifest.Name);
        if (_cakeContext.FileExists(harvestSummaryTempFile))
        {
            _log.Verbose("Cleaning orphan harvest-summary-tmp for {0}: {1}", manifest.Name, harvestSummaryTempFile);
            _cakeContext.DeleteFile(harvestSummaryTempFile);
        }
    }

    private async Task<DeploymentStatistics> ExecuteHarvestPipelineAsync(LibraryManifest manifest, DirectoryPath outputBase)
    {

        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(outputBase);

        var closureResult = await _binaryClosureWalker.BuildClosureAsync(manifest);
        closureResult.OnError(e => LogAndThrow("Binary closure", e, _log, manifest.Name));

        var validationResult = _dependencyPolicyValidator.Validate(closureResult.Closure, manifest);
        validationResult.OnError(e => LogAndThrowValidation(e, _log, manifest.Name));

        if (validationResult.ValidationSuccess.HasWarnings)
        {
            LogValidationWarnings(_log, manifest.Name, validationResult.ValidationSuccess.Warnings);
        }

        var plannerResult = await _artifactPlanner.CreatePlanAsync(manifest, closureResult.Closure, outputBase);
        plannerResult.OnError(e => LogAndThrow("Artifact planning", e, _log, manifest.Name));

        var copierResult = await _artifactDeployer.DeployArtifactsAsync(plannerResult.DeploymentPlan);
        copierResult.OnError(e => LogAndThrow("Artifact copying", e, _log, manifest.Name));

        var statistics = plannerResult.DeploymentPlan.Statistics;

        // G1 post-harvest invariant: a successful harvest must have produced at least one
        // primary binary. Defence-in-depth for the case where the walker and planner each
        // returned OK-shaped results but the resolved primary set ended up empty (silent
        // feature-flag degradation, partial vcpkg install, etc.). BinaryClosureWalker is
        // the primary guard, but this post-check ensures no downstream consumer ingests a
        // rid-status=true with zero primaries.
        if (statistics.PrimaryFiles.Count == 0)
        {
            var message =
                $"Harvest produced zero primary binaries for '{manifest.Name}' on {_runtimeProfile.Rid} " +
                $"(triplet '{_runtimeProfile.Triplet}'). Closure walker and planner returned success but " +
                "no primary files were deployed. Inspect vcpkg install output and manifest primary_binaries patterns.";
            _log.Error(message);
            throw new CakeException(message);
        }

        await GenerateRidStatusFileAsync(manifest, statistics, outputBase);
        return statistics;
    }

    private static void DisplayHarvestReportSummary(DeploymentStatistics stats)
    {
        DisplaySummaryPanel(stats);
        DisplayPrimaryFilesTable(stats);
        DisplayPackageBreakdown(stats);
        DisplayDetailPanel(stats);
    }

    private static void DisplaySummaryPanel(DeploymentStatistics stats)
    {
        var grid = new Grid()
            .AddColumn()
            .AddColumn();

        grid.AddRow("[bold]Library[/]", $"[white]{stats.LibraryName}[/]");
        grid.AddRow("[bold]Deployment Strategy[/]", $"[cyan]{DescribeDeploymentStrategy(stats.DeploymentStrategy)}[/]");
        grid.AddRow("[bold]Primary Files[/]", $"[lime]{stats.PrimaryFiles.Count}[/]");
        grid.AddRow("[bold]Runtime Dependencies[/]", $"[deepskyblue1]{stats.RuntimeFiles.Count}[/]");
        grid.AddRow("[bold]License Files[/]", $"[grey54]{stats.LicenseFiles.Count}[/]");
        grid.AddRow("[bold]Deployed Packages[/]", $"[white]{stats.DeployedPackages.Count}[/]");

        if (stats.FilteredPackages.Any())
        {
            grid.AddRow("[bold]Filtered Packages[/]", $"[yellow]{stats.FilteredPackages.Count}[/] (excluded from deployment)");
        }

        var infoPanel = new Panel(grid)
            .Header($"[bold yellow]{stats.LibraryName} � Deployment Summary[/]", Justify.Left)
            .BorderColor(Color.Grey);

        AnsiConsole.Write(infoPanel);
    }

    private static void DisplayPrimaryFilesTable(DeploymentStatistics stats)
    {
        if (!stats.PrimaryFiles.Any())
        {
            return;
        }

        var primaryTable = new Table()
            .RoundedBorder()
            .BorderColor(Color.Green)
            .AddColumn("[bold]Primary Files[/]")
            .AddColumn("[bold]Location[/]");

        foreach (var fileInfo in stats.PrimaryFiles.OrderBy(f => f.FilePath.GetFilename().FullPath, StringComparer.Ordinal))
        {
            primaryTable.AddRow($"[lime]{fileInfo.FilePath.GetFilename().FullPath}[/]", ToLocationText(fileInfo.DeploymentLocation));
        }

        AnsiConsole.Write(primaryTable);
    }

    private static void DisplayPackageBreakdown(DeploymentStatistics stats)
    {
        var packageTable = new Table()
            .RoundedBorder()
            .BorderColor(Color.Blue)
            .AddColumn("[bold]Package Type[/]")
            .AddColumn("[bold]Packages[/]");

        packageTable.AddRow("[deepskyblue1]Deployed[/]", $"[white]{FormatPackages(stats.DeployedPackages)}[/]");

        if (stats.FilteredPackages.Any())
        {
            packageTable.AddRow("[yellow]Filtered[/]", $"[grey]{FormatPackages(stats.FilteredPackages)}[/]");
        }

        AnsiConsole.Write(packageTable);
    }

    private static void DisplayDetailPanel(DeploymentStatistics stats)
    {
        if (!stats.RuntimeFiles.Any() && !stats.PrimaryFiles.Any() && !stats.LicenseFiles.Any())
        {
            return;
        }

        var detailTable = new Table()
            .RoundedBorder()
            .BorderColor(Color.Grey)
            .AddColumn("[bold]Type[/]")
            .AddColumn("[bold]File[/]")
            .AddColumn("[bold]Package[/]")
            .AddColumn("[bold]Location[/]");

        AddDetailRows(detailTable, "[lime]Primary[/]", stats.PrimaryFiles);
        AddDetailRows(detailTable, "[deepskyblue1]Runtime[/]", stats.RuntimeFiles);
        AddDetailRows(detailTable, "[grey54]License[/]", stats.LicenseFiles);

        var detailPanel = new Panel(detailTable)
            .Header($"[bold yellow]{stats.LibraryName} � Detailed File List[/]", Justify.Left)
            .BorderColor(Color.Grey);

        AnsiConsole.Write(detailPanel);
    }

    private static void AddDetailRows(Table detailTable, string typeLabel, IReadOnlyList<FileDeploymentInfo> files)
    {
        ArgumentNullException.ThrowIfNull(detailTable);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrEmpty(typeLabel);

        foreach (var fileInfo in files.OrderBy(f => f.PackageName, StringComparer.Ordinal).ThenBy(f => f.FilePath.GetFilename().FullPath, StringComparer.Ordinal))
        {
            detailTable.AddRow(
                typeLabel,
                $"[white]{fileInfo.FilePath.GetFilename().FullPath}[/]",
                $"[grey]{fileInfo.PackageName}[/]",
                ToLocationText(fileInfo.DeploymentLocation));
        }
    }

    private static string DescribeDeploymentStrategy(DeploymentStrategy strategy)
    {
        return strategy switch
        {
            DeploymentStrategy.DirectCopy => "Direct copy: All files ? filesystem",
            DeploymentStrategy.Archive => "Mixed: Binaries ? archive, licenses ? filesystem",
            _ => "Unknown",
        };
    }

    private static string FormatPackages(IEnumerable<string> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);

        var orderedPackages = packages.Order(StringComparer.Ordinal).ToList();
        return orderedPackages.Count == 0 ? "None" : string.Join(", ", orderedPackages);
    }

    private static string ToLocationText(DeploymentLocation deploymentLocation)
    {
        return deploymentLocation switch
        {
            DeploymentLocation.FileSystem => "[white]Filesystem[/]",
            DeploymentLocation.Archive => "[cyan]Archive[/]",
            _ => "[grey]Unknown[/]",
        };
    }

    private static void LogAndThrow(string phase, HarvestingError error, ICakeLog log, string libraryName)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrEmpty(phase);
        ArgumentException.ThrowIfNullOrEmpty(libraryName);

        log.Error("{0} failed for '{1}': {2}", phase, libraryName, error.Message);

        if (error.Exception is not null)
        {
            log.Verbose("Details: {0}", error.Exception);
        }

        throw new CakeException($"{phase} failed for '{libraryName}'. Use �verbosity=diagnostic for details. Error: {error.Message}");
    }

    private static void LogAndThrowValidation(ValidationError error, ICakeLog log, string libraryName)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrEmpty(libraryName);

        log.Error("Dependency policy validation failed for '{0}': {1}", libraryName, error.Message);

        foreach (var violation in error.Violations)
        {
            log.Error(
                "  - {0} (owner: {1}, origin: {2})",
                IoPath.GetFileName(violation.Path),
                violation.OwnerPackage,
                violation.OriginPackage);
        }

        throw new CakeException(
            $"Dependency policy validation failed for '{libraryName}'. " +
            $"Use �verbosity=diagnostic for details. Error: {error.Message}");
    }

    private static void LogValidationWarnings(ICakeLog log, string libraryName, IReadOnlyList<BinaryNode> warnings)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrEmpty(libraryName);
        ArgumentNullException.ThrowIfNull(warnings);

        log.Warning(
            "Dependency policy validation produced {0} warning(s) for '{1}' (non-blocking).",
            warnings.Count,
            libraryName);

        foreach (var warning in warnings)
        {
            log.Warning(
                "  - {0} (owner: {1}, origin: {2})",
                IoPath.GetFileName(warning.Path),
                warning.OwnerPackage,
                warning.OriginPackage);
        }
    }

    private async Task HandleKnownHarvestFailureAsync(LibraryManifest manifest, DirectoryPath outputBase, string errorMessage)
    {

        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(outputBase);
        ArgumentException.ThrowIfNullOrEmpty(errorMessage);

        await GenerateErrorRidStatusFileAsync(manifest, outputBase, errorMessage);
        AnsiConsole.Write(new Rule($"[red]Failed Harvest: {manifest.Name}[/]").RuleStyle("grey"));
    }

    private async Task HandleOperationalHarvestFailureAsync(LibraryManifest manifest, DirectoryPath outputBase, Exception ex)
    {

        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(outputBase);
        ArgumentNullException.ThrowIfNull(ex);

        _log.Error("Unexpected operational error during harvest of {0}: {1}", manifest.Name, ex.Message);
        _log.Verbose("Harvest error details: {0}", ex);

        await GenerateErrorRidStatusFileAsync(manifest, outputBase, ex.Message);
        AnsiConsole.Write(new Rule($"[red]Failed Harvest: {manifest.Name}[/]").RuleStyle("grey"));
    }

    /// <summary>
    /// Generates a RID-specific status file for later consolidation into the harvest manifest.
    /// This allows CI matrix jobs to run per library+RID while still generating a complete
    /// library-wide harvest manifest in a subsequent consolidation step.
    /// </summary>
    private async Task GenerateRidStatusFileAsync(LibraryManifest manifest, DeploymentStatistics statistics, DirectoryPath outputBase)
    {

        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(outputBase);

        try
        {
            var ridStatusDir = _pathService.GetHarvestLibraryRidStatusDir(manifest.Name);
            _cakeContext.EnsureDirectoryExists(ridStatusDir);

            var ridStatus = new RidHarvestStatus
            {
                LibraryName = manifest.Name,
                Rid = _runtimeProfile.Rid,
                Triplet = _runtimeProfile.Triplet,
                Success = true,
                ErrorMessage = null,
                Timestamp = DateTimeOffset.UtcNow,
                Statistics = new HarvestStatistics
                {
                    PrimaryFilesCount = statistics.PrimaryFiles.Count,
                    RuntimeFilesCount = statistics.RuntimeFiles.Count,
                    LicenseFilesCount = statistics.LicenseFiles.Count,
                    DeployedPackagesCount = statistics.DeployedPackages.Count,
                    FilteredPackagesCount = statistics.FilteredPackages.Count,
                    DeploymentStrategy = statistics.DeploymentStrategy.ToString()
                },
            };

            var statusFilePath = _pathService.GetHarvestLibraryRidStatusFile(manifest.Name, _runtimeProfile.Rid);

            await _cakeContext.WriteJsonAsync(statusFilePath, ridStatus, JsonOptions);

            _log.Information("Generated RID status file: {0}", statusFilePath);
        }
        catch (Exception ex) when (IsOperationalHarvestException(ex))
        {
            LogRidStatusGenerationFailure(manifest, ex);

            // Generate an error status file so consolidation knows this RID failed
            await GenerateErrorRidStatusFileAsync(manifest, outputBase, ex.Message);
        }
    }

    /// <summary>
    /// Generates an error RID status file when harvest fails for a specific RID.
    /// </summary>
    private async Task GenerateErrorRidStatusFileAsync(LibraryManifest manifest, DirectoryPath outputBase, string errorMessage)
    {

        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(outputBase);
        ArgumentException.ThrowIfNullOrEmpty(errorMessage);

        try
        {
            var ridStatusDir = _pathService.GetHarvestLibraryRidStatusDir(manifest.Name);
            _cakeContext.EnsureDirectoryExists(ridStatusDir);

            var ridStatus = new RidHarvestStatus
            {
                LibraryName = manifest.Name,
                Rid = _runtimeProfile.Rid,
                Triplet = _runtimeProfile.Triplet,
                Success = false,
                ErrorMessage = errorMessage,
                Timestamp = DateTimeOffset.UtcNow,
                Statistics = null,
            };

            var statusFilePath = _pathService.GetHarvestLibraryRidStatusFile(manifest.Name, _runtimeProfile.Rid);

            await _cakeContext.WriteJsonAsync(statusFilePath, ridStatus, JsonOptions);

            _log.Information("Generated error RID status file: {0}", statusFilePath);
        }
        catch (Exception ex) when (IsOperationalHarvestException(ex))
        {
            LogErrorRidStatusGenerationFailure(manifest, ex);
        }
    }

    private static bool IsOperationalHarvestException(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or JsonException;
    }

    private void LogRidStatusGenerationFailure(LibraryManifest manifest, Exception ex)
    {

        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(ex);

        _log.Warning("Failed to generate RID status file for {0}/{1}: {2}", manifest.Name, _runtimeProfile.Rid, ex.Message);
        _log.Verbose("RID status file generation error details: {0}", ex);
    }

    private void LogErrorRidStatusGenerationFailure(LibraryManifest manifest, Exception ex)
    {

        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(ex);

        _log.Error("Failed to generate error RID status file for {0}/{1}: {2}", manifest.Name, _runtimeProfile.Rid, ex.Message);
        _log.Verbose("Error RID status file generation error details: {0}", ex);
    }
}
