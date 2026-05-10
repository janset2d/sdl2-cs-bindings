using System.Diagnostics.CodeAnalysis;
using Build.Host;
using Build.Host.Cake;
using Build.Host.Configuration;
using Build.Host.Paths;
using Build.Integrations.DotNet;
using Build.Repositories;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Build.Targets.PackageConsumerSmoke.Reporting;
using Build.Targets.PackageConsumerSmoke.Services;
using Build.Validation.Conventions;
using Build.Validation.Packaging;
using Build.Versioning;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Targets.PackageConsumerSmoke;

[TaskName("PackageConsumerSmoke")]
[TaskDescription("Restores and runs the package consumer smoke app against artifacts/packages")]
public sealed class PackageConsumerSmokeTask : AsyncFrostingTask<BuildContext>
{
    private readonly IPackageConsumerSmokePreconditionsValidator _preconditionsValidator;
    private readonly DotNetSmokeRunner _runner;
    private readonly MonoAvailabilityProbe _monoProbe;
    private readonly PackageConsumerSmokeReporter _reporter;
    private readonly IProjectMetadataReader _projectMetadataReader;
    private readonly IDotNetRuntimeEnvironment _dotNetRuntimeEnvironment;
    private readonly IVersionFileRepository _versionFileRepository;
    private readonly IRuntimeProfile _runtimeProfile;
    private readonly IPathService _pathService;
    private readonly ManifestConfig _manifestConfig;

    public PackageConsumerSmokeTask(
        IPackageConsumerSmokePreconditionsValidator preconditionsValidator,
        DotNetSmokeRunner runner,
        MonoAvailabilityProbe monoProbe,
        PackageConsumerSmokeReporter reporter,
        IProjectMetadataReader projectMetadataReader,
        IDotNetRuntimeEnvironment dotNetRuntimeEnvironment,
        IVersionFileRepository versionFileRepository,
        IRuntimeProfile runtimeProfile,
        IPathService pathService,
        ManifestConfig manifestConfig)
    {
        _preconditionsValidator = preconditionsValidator ?? throw new ArgumentNullException(nameof(preconditionsValidator));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _monoProbe = monoProbe ?? throw new ArgumentNullException(nameof(monoProbe));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        _projectMetadataReader = projectMetadataReader ?? throw new ArgumentNullException(nameof(projectMetadataReader));
        _dotNetRuntimeEnvironment = dotNetRuntimeEnvironment ?? throw new ArgumentNullException(nameof(dotNetRuntimeEnvironment));
        _versionFileRepository = versionFileRepository ?? throw new ArgumentNullException(nameof(versionFileRepository));
        _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    }

    private sealed record SmokePackage(string FamilyName, string ManagedPackageId, string NativePackageId, string VersionPropertyName)
    {
        public static SmokePackage FromFamily(PackageFamilyConfig family)
        {
            ArgumentNullException.ThrowIfNull(family);
            return new SmokePackage(
                FamilyName: family.Name,
                ManagedPackageId: FamilyIdentifierConventions.ManagedPackageId(family.Name),
                NativePackageId: FamilyIdentifierConventions.NativePackageId(family.Name),
                VersionPropertyName: FamilyIdentifierConventions.VersionPropertyName(family.Name));
        }
    }

    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "Linear orchestration: guards + scope/preconditions + per-TFM smoke. Splitting hurts traceability of the operator-visible pass/fail sequence.")]
    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.VersionsFilePath is null)
        {
            throw new CakeException(
                "PackageConsumerSmoke requires --versions-file <path>. " +
                "Run --target ResolveVersions first to produce a versions.json, " +
                "then re-run with --versions-file artifacts/resolve-versions/versions.json.");
        }

        var familyVersions = _versionFileRepository.Load(context.VersionsFilePath);
        if (familyVersions.Count == 0)
        {
            throw new CakeException(
                "PackageConsumerSmoke requires a non-empty version mapping. " +
                "Re-run --target ResolveVersionsFromManifest or --target ResolveVersionsFromExplicit.");
        }

        var smokeProject = _pathService.PackageConsumerSmokeProject;
        var compileSanityProject = _pathService.CompileSanityProject;
        var feedPath = _pathService.PackagesOutput;

        var preconditionReport = _preconditionsValidator.Validate(smokeProject, compileSanityProject, feedPath);
        if (!preconditionReport.IsValid)
        {
            throw new CakeException(
                $"PackageConsumerSmoke preconditions failed:{Environment.NewLine}" +
                string.Join(Environment.NewLine, preconditionReport.Errors.Select(e => $"  [{e.Code}] {e.Message}")));
        }

        var smokePackages = ResolveSmokePackages();
        EnsureSelectionSupportsCurrentSmokeScope(smokePackages, familyVersions);
        await EnsureSmokeCsprojsMatchManifestScopeAsync(context, smokePackages, default);
        EnsurePackageArtifactsExist(context, smokePackages, familyVersions, feedPath);

        _reporter.LogStarting(_runtimeProfile.Rid, smokePackages.Select(p => p.FamilyName).ToList());

        // Kill any lingering build-server processes (VBCSCompiler, MSBuild worker nodes,
        // Razor) from prior invocations before we try to delete bin/obj — those servers
        // hold file handles on compiled assemblies and block Directory.Delete with Access
        // Denied on Windows. Linux/macOS show the same lingering /nodemode:1 dotnet
        // processes but usually don't lock filesystem paths; shutdown still keeps memory
        // usage in check.
        _runner.ShutdownBuildServers(_pathService.RepoRoot);

        var workingRoot = _pathService.ArtifactsDir.Combine("package-consumer-smoke");
        var packagesCache = workingRoot.Combine("packages-cache");

        DeleteDirectoryIfExists(context, workingRoot);
        DeleteDirectoryIfExists(context, smokeProject.GetDirectory().Combine("bin"));
        DeleteDirectoryIfExists(context, smokeProject.GetDirectory().Combine("obj"));
        DeleteDirectoryIfExists(context, compileSanityProject.GetDirectory().Combine("bin"));
        DeleteDirectoryIfExists(context, compileSanityProject.GetDirectory().Combine("obj"));

        context.EnsureDirectoryExists(workingRoot);
        context.EnsureDirectoryExists(packagesCache);

        var metadataResult = await _projectMetadataReader.ReadAsync(smokeProject);
        if (metadataResult.IsFailure)
        {
            throw new CakeException(
                $"PackageConsumerSmoke could not resolve TFMs for '{smokeProject.FullPath}'. Error: {metadataResult.Error.Message}");
        }

        var projectMetadata = metadataResult.Value;

        // Compile-only sanity for the netstandard2.0 consumer slice. netstandard2.0 is a
        // contract, not a runtime — if this library compiles against our package, the
        // netstandard2.0 consumer surface is validated.
        RunCompileSanity(compileSanityProject, smokePackages, familyVersions, packagesCache, feedPath, context.BuildConfiguration);

        var runtimeEnvironmentDelta = await _dotNetRuntimeEnvironment.ResolveAsync(
            _runtimeProfile.Rid,
            projectMetadata.TargetFrameworks);

        var ranCount = 0;
        var skippedCount = 0;
        foreach (var tfm in projectMetadata.TargetFrameworks)
        {
            if (ShouldSkipTfm(tfm, out var skipReason))
            {
                _reporter.ReportSkippedTfm(tfm, skipReason);
                skippedCount++;
                continue;
            }

            // TUnit + Microsoft Testing Platform on Windows can leave enough CLI-side
            // state behind after one TFM run that the next TFM, especially net4x,
            // intermittently fails despite passing when invoked in isolation. Reset
            // build servers between TFMs to keep the multi-target sequence stable.
            _runner.ShutdownBuildServers(_pathService.RepoRoot);
            _reporter.StartTfm(tfm);

            RunSmokeForTfm(
                smokeProject,
                smokePackages,
                familyVersions,
                packagesCache,
                feedPath,
                _runtimeProfile.Rid,
                tfm,
                runtimeEnvironmentDelta,
                context.BuildConfiguration);

            _reporter.FinishTfm(tfm);
            ranCount++;
        }

        _reporter.LogCompleted(tfmsRun: ranCount, tfmsSkipped: skippedCount);
    }

    private List<SmokePackage> ResolveSmokePackages()
    {
        var concreteFamilies = _manifestConfig.PackageFamilies
            .Where(HasConcreteProjects)
            .OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (concreteFamilies.Count == 0)
        {
            throw new CakeException(
                "PackageConsumerSmoke cannot run: manifest.json declares no package families with both managed_project and native_project. Smoke requires at least one concrete family.");
        }

        return concreteFamilies.Select(SmokePackage.FromFamily).ToList();
    }

    private static bool HasConcreteProjects(PackageFamilyConfig family) =>
        !string.IsNullOrWhiteSpace(family.ManagedProject) && !string.IsNullOrWhiteSpace(family.NativeProject);

    private static void EnsureSelectionSupportsCurrentSmokeScope(IReadOnlyList<SmokePackage> smokePackages, PackageFamilyVersionSet explicitVersions)
    {
        if (explicitVersions.Count == 0)
        {
            return;
        }

        var missingFamilies = smokePackages
            .Where(p => !explicitVersions.Contains(new PackageFamilyId(p.FamilyName)))
            .Select(p => p.FamilyName)
            .ToList();

        if (missingFamilies.Count != 0)
        {
            var scope = string.Join(", ", smokePackages.Select(p => $"'{p.FamilyName}'"));
            throw new CakeException(
                $"PackageConsumerSmoke validates the concrete package set {scope}. Either include the full smoke scope in --explicit-version, or use --versions-file <path>. Missing: {string.Join(", ", missingFamilies)}.");
        }
    }

    private async Task EnsureSmokeCsprojsMatchManifestScopeAsync(BuildContext context, IReadOnlyList<SmokePackage> smokePackages, CancellationToken ct)
    {
        var expectedManagedPackageIds = smokePackages.Select(p => p.ManagedPackageId).ToList();

        var consumerProjects = new (FilePath ProjectPath, string Description)[]
        {
            (_pathService.PackageConsumerSmokeProject, "PackageConsumer.Smoke"),
            (_pathService.CompileSanityProject, "Compile.NetStandard"),
        };

        foreach (var (projectPath, description) in consumerProjects)
        {
            ct.ThrowIfCancellationRequested();

            if (!context.FileExists(projectPath))
            {
                throw new CakeException(
                    $"PackageConsumerSmoke cannot find smoke csproj '{projectPath.FullPath}' ({description}).");
            }

            var csprojXml = await context.ReadAllTextAsync(projectPath);

            var comparison = SmokeScopeComparator.Compare(csprojXml, expectedManagedPackageIds);
            if (comparison.IsMatch)
            {
                continue;
            }

            var details = new List<string>();
            if (comparison.Missing.Count > 0)
            {
                details.Add($"missing PackageReference(s): {string.Join(", ", comparison.Missing)}");
            }
            if (comparison.Unexpected.Count > 0)
            {
                details.Add($"unexpected Janset.SDL.* PackageReference(s): {string.Join(", ", comparison.Unexpected)}");
            }

            throw new CakeException(
                $"PackageConsumerSmoke scope drift in '{description}' ({projectPath.FullPath}): {string.Join("; ", details)}.");
        }
    }

    private static void EnsurePackageArtifactsExist(BuildContext context, IReadOnlyList<SmokePackage> smokePackages, PackageFamilyVersionSet familyVersions, DirectoryPath feedPath)
    {
        foreach (var pkg in smokePackages)
        {
            var version = familyVersions.RequireVersion(new PackageFamilyId(pkg.FamilyName)).ToNormalizedString();
            EnsurePackageExists(context, pkg.ManagedPackageId, version, feedPath);
            EnsurePackageExists(context, pkg.NativePackageId, version, feedPath);
        }
    }

    private static void EnsurePackageExists(BuildContext context, string packageId, string version, DirectoryPath feedPath)
    {
        var packagePath = feedPath.CombineWithFilePath($"{packageId}.{version}.nupkg");
        if (!context.FileExists(packagePath))
        {
            throw new CakeException(
                $"PackageConsumerSmoke expected local feed package '{packagePath.GetFilename().FullPath}' in '{feedPath.FullPath}', but it was not found. Run Package first or use a matching --explicit-version entry.");
        }
    }

    private void RunCompileSanity(
        FilePath projectPath,
        IReadOnlyList<SmokePackage> smokePackages,
        PackageFamilyVersionSet familyVersions,
        DirectoryPath packagesCache,
        DirectoryPath feedPath,
        string buildConfiguration)
    {
        var arguments = new ProcessArgumentBuilder()
            .Append("build")
            .AppendQuoted(projectPath.FullPath)
            .Append("-c")
            .Append(buildConfiguration);

        AppendFeedArguments(arguments, packagesCache, feedPath);
        AppendSmokePackageVersionProperties(arguments, smokePackages, familyVersions);

        _runner.RunCompileSanity("compile-sanity netstandard2.0 consumer", arguments, _pathService.RepoRoot);
    }

    private void RunSmokeForTfm(
        FilePath projectPath,
        IReadOnlyList<SmokePackage> smokePackages,
        PackageFamilyVersionSet familyVersions,
        DirectoryPath packagesCache,
        DirectoryPath feedPath,
        string rid,
        string tfm,
        IReadOnlyDictionary<string, string> runtimeEnvironment,
        string buildConfiguration)
    {
        var arguments = new ProcessArgumentBuilder()
            .Append("test")
            .Append("--project")
            .AppendQuoted(projectPath.FullPath)
            .Append("-c")
            .Append(buildConfiguration)
            .Append("-f")
            .Append(tfm)
            .Append("-r")
            .Append(rid)
            .Append("-p:UseSharedCompilation=false");

        // .NET Framework + AnyCPU + native package presence triggers SDK's auto-x86
        // RuntimeIdentifierInference. Forwarding -p:Platform=<arch> alongside -r <rid>
        // makes the smoke runner's intent explicit. Only applies on Windows + net4x.
        AppendNet4xPlatformArgument(arguments, rid, tfm);

        AppendFeedArguments(arguments, packagesCache, feedPath);
        AppendSmokePackageVersionProperties(arguments, smokePackages, familyVersions);

        _runner.RunSmokeForTfm($"test package-smoke ({tfm})", arguments, _pathService.RepoRoot, runtimeEnvironment);
    }

    private static void AppendNet4xPlatformArgument(ProcessArgumentBuilder arguments, string rid, string tfm)
    {
        if (!tfm.StartsWith("net4", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var platform = rid switch
        {
            "win-x64" => "x64",
            "win-x86" => "x86",
            "win-arm64" => "ARM64",
            _ => null,
        };

        if (platform is null)
        {
            return;
        }

        arguments.Append($"-p:Platform={platform}");
    }

    private static void AppendFeedArguments(ProcessArgumentBuilder arguments, DirectoryPath packagesCache, DirectoryPath feedPath)
    {
        arguments
            .AppendQuoted($"-p:LocalPackageFeed={feedPath.FullPath}")
            .AppendQuoted($"-p:RestorePackagesPath={packagesCache.FullPath}");
    }

    private static void AppendSmokePackageVersionProperties(
        ProcessArgumentBuilder arguments,
        IReadOnlyList<SmokePackage> smokePackages,
        PackageFamilyVersionSet familyVersions)
    {
        foreach (var pkg in smokePackages)
        {
            if (familyVersions.TryGetVersion(new PackageFamilyId(pkg.FamilyName), out var version))
            {
                arguments.Append($"-p:{pkg.VersionPropertyName}={version.ToNormalizedString()}");
            }
        }
    }

    private bool ShouldSkipTfm(string tfm, out string reason)
    {
        reason = string.Empty;

        if (!tfm.StartsWith("net4", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        if (OperatingSystem.IsMacOS())
        {
            if (_monoProbe.IsMonoAvailable())
            {
                return false;
            }

            reason = $"TFM '{tfm}' runtime execution is skipped on macOS: `mono` binary not found in $PATH. " +
                     "Install classic Mono to enable net462 runtime coverage. Compile-time coverage of net462 still runs via Microsoft.NETFramework.ReferenceAssemblies.";
            return true;
        }

        reason = $"TFM '{tfm}' runtime execution is skipped on Linux: Mono 6.12 cannot host TUnit (MissingMethodException in Microsoft Testing Platform discovery). Compile-time coverage of net462 still runs via Microsoft.NETFramework.ReferenceAssemblies.";
        return true;
    }

    private static void DeleteDirectoryIfExists(BuildContext context, DirectoryPath directoryPath)
    {
        if (context.DirectoryExists(directoryPath))
        {
            context.DeleteDirectory(directoryPath, new DeleteDirectorySettings { Recursive = true, Force = true });
        }
    }
}
