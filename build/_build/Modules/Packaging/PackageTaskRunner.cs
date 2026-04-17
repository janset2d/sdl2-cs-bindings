using Build.Context;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Packaging.Models;
using Build.Modules.Packaging.Results;
using Cake.Common;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Modules.Packaging;

public sealed class PackageTaskRunner(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    DotNetBuildConfiguration dotNetBuildConfiguration,
    PackageBuildConfiguration packageBuildConfiguration,
    IPackageFamilySelector packageFamilySelector,
    IPackageVersionResolver packageVersionResolver,
    IDotNetPackInvoker dotNetPackInvoker,
    IProjectMetadataReader projectMetadataReader,
    IPackageOutputValidator packageOutputValidator) : IPackageTaskRunner
{
    private static readonly string[] PayloadDirectories = ["runtimes", "licenses"];

    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly DotNetBuildConfiguration _dotNetBuildConfiguration = dotNetBuildConfiguration ?? throw new ArgumentNullException(nameof(dotNetBuildConfiguration));
    private readonly PackageBuildConfiguration _packageBuildConfiguration = packageBuildConfiguration ?? throw new ArgumentNullException(nameof(packageBuildConfiguration));
    private readonly IPackageFamilySelector _packageFamilySelector = packageFamilySelector ?? throw new ArgumentNullException(nameof(packageFamilySelector));
    private readonly IPackageVersionResolver _packageVersionResolver = packageVersionResolver ?? throw new ArgumentNullException(nameof(packageVersionResolver));
    private readonly IDotNetPackInvoker _dotNetPackInvoker = dotNetPackInvoker ?? throw new ArgumentNullException(nameof(dotNetPackInvoker));
    private readonly IProjectMetadataReader _projectMetadataReader = projectMetadataReader ?? throw new ArgumentNullException(nameof(projectMetadataReader));
    private readonly IPackageOutputValidator _packageOutputValidator = packageOutputValidator ?? throw new ArgumentNullException(nameof(packageOutputValidator));

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var versionResult = _packageVersionResolver.Resolve(_packageBuildConfiguration.FamilyVersion);
        if (versionResult.IsError())
        {
            throw new CakeException(versionResult.PackageVersionResolutionError.Message);
        }

        var selectionResult = _packageFamilySelector.Select(_packageBuildConfiguration.Families);
        if (selectionResult.IsError())
        {
            throw new CakeException(selectionResult.PackageFamilySelectionError.Message);
        }

        var version = versionResult.PackageVersion.Value;
        var families = selectionResult.Selection.Families;
        var expectedCommitSha = ResolveHeadCommitSha();

        _cakeContext.EnsureDirectoryExists(_pathService.PackagesOutput);

        foreach (var family in families)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PackFamilyAsync(family, version, expectedCommitSha, cancellationToken);
        }
    }

    private async Task PackFamilyAsync(PackageFamilyConfig family, string version, string expectedCommitSha, CancellationToken cancellationToken)
    {
        var managedProjectPath = ResolveProjectPath(family.ManagedProject, family.Name, "managed_project");
        var nativeProjectPath = ResolveProjectPath(family.NativeProject, family.Name, "native_project");

        _log.Information("Packing family '{0}' at version '{1}'.", family.Name, version);

        await EnsureHarvestOutputReadyAsync(family, cancellationToken);

        var nativePayloadSource = _pathService.HarvestOutput.Combine(family.LibraryRef);

        // Post-S1 (2026-04-17): within-family dependency is SkiaSharp-style minimum range.
        // No exact-pin CPM plumbing, no per-family MSBuild property. Each pack invocation
        // gets $(Version) and (for the native only) $(NativePayloadSource). Managed's
        // ProjectReference to the native emits as a standard `>=` dependency in the nuspec.
        // Drift protection is orchestration-time: both packs carry identical `version` and
        // the post-pack validator asserts the emitted `<version>` elements match (G23).
        var nativeInvocation = new DotNetPackInvocation(
            Configuration: _dotNetBuildConfiguration.Configuration,
            Version: version,
            NativePayloadSource: nativePayloadSource);

        var managedInvocation = nativeInvocation with { NativePayloadSource = null };

        var nativePackResult = _dotNetPackInvoker.Pack(nativeProjectPath, nativeInvocation, noRestore: false, noBuild: false);
        if (nativePackResult.IsError())
        {
            LogAndThrowPackError(family, nativePackResult.DotNetPackError);
        }

        var managedPackResult = _dotNetPackInvoker.Pack(managedProjectPath, managedInvocation, noRestore: false, noBuild: false);
        if (managedPackResult.IsError())
        {
            LogAndThrowPackError(family, managedPackResult.DotNetPackError);
        }

        var metadataResult = await _projectMetadataReader.ReadAsync(managedProjectPath, cancellationToken);
        if (metadataResult.IsError())
        {
            var error = metadataResult.ProjectMetadataError;
            _log.Error("Project metadata resolution failed for family '{0}': {1}", family.Name, error.Message);
            throw new CakeException(
                $"Project metadata resolution failed for family '{family.Name}'. Error: {error.Message}");
        }

        var artifacts = CreateArtifacts(family, version);
        var validationResult = await _packageOutputValidator.ValidateAsync(family, artifacts, version, expectedCommitSha, metadataResult.ProjectMetadata);

        LogValidationDiagnostics(family, validationResult.Validation);

        if (validationResult.IsError())
        {
            throw new CakeException(validationResult.PackageValidationError.Message);
        }

        _log.Information(
            "Packed family '{0}': {1}, {2}, {3}",
            family.Name,
            artifacts.NativePackage.GetFilename().FullPath,
            artifacts.ManagedPackage.GetFilename().FullPath,
            artifacts.ManagedSymbolsPackage.GetFilename().FullPath);
    }

    private void LogAndThrowPackError(PackageFamilyConfig family, DotNetPackError error)
    {
        _log.Error("dotnet pack failed for family '{0}': {1}", family.Name, error.Message);
        if (error.Exception is not null)
        {
            _log.Verbose("Details: {0}", error.Exception);
        }

        throw new CakeException($"dotnet pack failed for family '{family.Name}'. Error: {error.Message}");
    }

    private void LogValidationDiagnostics(PackageFamilyConfig family, PackageValidation validation)
    {
        foreach (var check in validation.Checks.Where(check => check.IsError))
        {
            _log.Error(
                "  - [{0}] {1}",
                check.Kind,
                check.ErrorMessage ?? "<no message>");
        }

        if (!validation.HasErrors)
        {
            _log.Verbose(
                "Family '{0}' post-pack validation: {1} check(s) passed.",
                family.Name,
                validation.Checks.Count);
        }
    }

    private async Task EnsureHarvestOutputReadyAsync(PackageFamilyConfig family, CancellationToken cancellationToken)
    {
        var harvestDirectory = _pathService.HarvestOutput.Combine(family.LibraryRef);
        var manifestPath = harvestDirectory.CombineWithFilePath("harvest-manifest.json");

        if (!_cakeContext.FileExists(manifestPath))
        {
            throw new CakeException(
                $"Package task cannot pack family '{family.Name}' because harvest manifest '{manifestPath.FullPath}' is missing. Run Harvest + ConsolidateHarvest for library '{family.LibraryRef}' first.");
        }

        var harvestManifest = await _cakeContext.ToJsonAsync<HarvestManifest>(manifestPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (harvestManifest.Summary.SuccessfulRids == 0)
        {
            throw new CakeException(
                $"Package task cannot pack family '{family.Name}' because harvest manifest '{manifestPath.FullPath}' reports zero successful RIDs.");
        }

        foreach (var payloadDirectory in PayloadDirectories)
        {
            var payloadSource = harvestDirectory.Combine(payloadDirectory);
            if (!_cakeContext.DirectoryExists(payloadSource))
            {
                throw new CakeException(
                    $"Package task cannot pack family '{family.Name}' because harvest payload directory '{payloadSource.FullPath}' is missing.");
            }

            var payloadFiles = _cakeContext.GetFiles($"{payloadSource}/**/*");
            if (payloadFiles.Count == 0)
            {
                throw new CakeException(
                    $"Package task cannot pack family '{family.Name}' because harvest payload directory '{payloadSource.FullPath}' is empty.");
            }
        }

        var successfulRids = harvestManifest.Rids
            .Where(ridStatus => ridStatus.Success)
            .Select(ridStatus => ridStatus.Rid)
            .OrderBy(rid => rid, StringComparer.OrdinalIgnoreCase);

        _log.Information("Family '{0}' will pack harvest payload for successful RIDs: {1}", family.Name, string.Join(", ", successfulRids));
    }

    private PackageArtifacts CreateArtifacts(PackageFamilyConfig family, string version)
    {
        var managedPackageId = Preflight.FamilyIdentifierConventions.ManagedPackageId(family.Name);
        var nativePackageId = Preflight.FamilyIdentifierConventions.NativePackageId(family.Name);

        return new PackageArtifacts(
            ManagedPackage: _pathService.PackagesOutput.CombineWithFilePath($"{managedPackageId}.{version}.nupkg"),
            ManagedSymbolsPackage: _pathService.PackagesOutput.CombineWithFilePath($"{managedPackageId}.{version}.snupkg"),
            NativePackage: _pathService.PackagesOutput.CombineWithFilePath($"{nativePackageId}.{version}.nupkg"));
    }

    private FilePath ResolveProjectPath(string? relativePath, string familyName, string manifestFieldName)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new CakeException($"Family '{familyName}' is missing manifest field '{manifestFieldName}'.");
        }

        return _pathService.RepoRoot.CombineWithFilePath(new FilePath(relativePath));
    }

    private string ResolveHeadCommitSha()
    {
        var process = _cakeContext.StartAndReturnProcess(
            "git",
            new ProcessSettings
            {
                Arguments = "rev-parse HEAD",
                WorkingDirectory = _pathService.RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Silent = true,
            });

        process.WaitForExit();
        var exitCode = process.GetExitCode();
        var output = process.GetStandardOutput()?.ToList() ?? [];
        var error = process.GetStandardError()?.ToList() ?? [];

        if (exitCode == 0 && output.Count != 0)
        {
            return output[0].Trim();
        }

        var combined = string.Join(Environment.NewLine, output.Concat(error));
        throw new CakeException($"Package task could not resolve git HEAD commit SHA. git rev-parse HEAD failed.{Environment.NewLine}{combined}");
    }
}
