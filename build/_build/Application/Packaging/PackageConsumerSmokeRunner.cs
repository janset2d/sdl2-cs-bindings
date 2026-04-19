using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Packaging;
using Build.Domain.Paths;
using Build.Domain.Preflight;
using Build.Domain.Runtime;
using Build.Infrastructure.DotNet;
using Build.Infrastructure.Paths;
using Cake.Common;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Application.Packaging;

public sealed class PackageConsumerSmokeRunner(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    IRuntimeProfile runtimeProfile,
    ManifestConfig manifestConfig,
    DotNetBuildConfiguration dotNetBuildConfiguration,
    PackageBuildConfiguration packageBuildConfiguration,
    IPackageVersionResolver packageVersionResolver,
    IProjectMetadataReader projectMetadataReader) : IPackageConsumerSmokeRunner
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly DotNetBuildConfiguration _dotNetBuildConfiguration = dotNetBuildConfiguration ?? throw new ArgumentNullException(nameof(dotNetBuildConfiguration));
    private readonly PackageBuildConfiguration _packageBuildConfiguration = packageBuildConfiguration ?? throw new ArgumentNullException(nameof(packageBuildConfiguration));
    private readonly IPackageVersionResolver _packageVersionResolver = packageVersionResolver ?? throw new ArgumentNullException(nameof(packageVersionResolver));
    private readonly IProjectMetadataReader _projectMetadataReader = projectMetadataReader ?? throw new ArgumentNullException(nameof(projectMetadataReader));

    /// <summary>
    /// Concrete family descriptor for smoke: derived from <c>manifest.json</c> package_families
    /// filtered by presence of managed + native project (same <c>HasConcreteProjects</c> rule
    /// used by <c>PackageFamilySelector</c>). Naming conventions come from
    /// <see cref="FamilyIdentifierConventions"/> so the runner, the shared smoke MSBuild
    /// props, and the csproj PackageReference entries stay aligned via a single convention.
    /// </summary>
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

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var smokePackages = ResolveSmokePackages();
        EnsureSelectionSupportsCurrentSmokeScope(smokePackages);
        await EnsureSmokeCsprojsMatchManifestScopeAsync(smokePackages, cancellationToken);

        var version = ResolveSmokeVersionAndEnsureFeed(smokePackages);

        // Kill any lingering build-server processes (VBCSCompiler, MSBuild worker nodes with
        // /nodeReuse:true, Razor) from prior invocations before we try to delete bin/obj —
        // those servers hold file handles on compiled assemblies and block Directory.Delete
        // with Access Denied on Windows. Linux/macOS show the same lingering /nodemode:1
        // dotnet processes but usually don't lock filesystem paths; shutdown still keeps
        // memory usage in check. See Known Gotchas in cross-platform-smoke-validation.md.
        ShutdownDotNetBuildServers();

        var smokeProject = _pathService.PackageConsumerSmokeProject;
        var compileSanityProject = _pathService.CompileSanityProject;

        var workingRoot = _pathService.ArtifactsDir.Combine("package-consumer-smoke");
        var packagesCache = workingRoot.Combine("packages-cache");

        // Clean slate: obj/bin on the consumer projects plus the isolated package cache.
        DeleteDirectoryIfExists(workingRoot);
        DeleteDirectoryIfExists(smokeProject.GetDirectory().Combine("bin"));
        DeleteDirectoryIfExists(smokeProject.GetDirectory().Combine("obj"));
        DeleteDirectoryIfExists(compileSanityProject.GetDirectory().Combine("bin"));
        DeleteDirectoryIfExists(compileSanityProject.GetDirectory().Combine("obj"));

        _cakeContext.EnsureDirectoryExists(workingRoot);
        _cakeContext.EnsureDirectoryExists(packagesCache);

        // 1. Compile-only sanity for the netstandard2.0 consumer slice.
        //    netstandard2.0 is a contract, not a runtime — if this library compiles
        //    against our package, the netstandard2.0 consumer surface is validated.
        RunCompileSanity(compileSanityProject, smokePackages, version, packagesCache);

        // 2. Per-TFM TUnit smoke for executable TFMs. TFM list comes from MSBuild
        //    evaluation of the smoke csproj (inherits $(ExecutableTargetFrameworks)
        //    from root Directory.Build.props), so adding a new TFM at root
        //    automatically expands the smoke matrix here with no extra wiring.
        var metadataResult = await _projectMetadataReader.ReadAsync(smokeProject, cancellationToken);
        if (metadataResult.IsError())
        {
            var error = metadataResult.ProjectMetadataError;
            _log.Error("PackageConsumerSmoke could not resolve TFMs for '{0}': {1}", smokeProject.FullPath, error.Message);
            throw new CakeException($"PackageConsumerSmoke could not resolve TFMs for '{smokeProject.FullPath}'. Error: {error.Message}");
        }

        foreach (var tfm in metadataResult.ProjectMetadata.TargetFrameworks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ShouldSkipTfm(tfm, out var skipReason))
            {
                _log.Warning("Skipping package-smoke for TFM '{0}': {1}", tfm, skipReason);
                continue;
            }

            // TUnit + Microsoft Testing Platform on Windows can leave enough CLI-side
            // state behind after one TFM run that the next TFM, especially net4x,
            // intermittently fails despite passing when invoked in isolation.
            // Reset build servers between TFMs to keep the multi-target sequence stable.
            ShutdownDotNetBuildServers();

            RunSmokeForTfm(smokeProject, smokePackages, version, packagesCache, tfm);
        }
    }

    /// <summary>
    /// Resolve the concrete smoke scope from <c>manifest.json</c>: every family that
    /// declares both managed_project and native_project (i.e., packs a real nupkg today).
    /// Families still in placeholder state (e.g., <c>sdl2-net</c> as of 2026-04-18) are
    /// excluded automatically without a hardcoded list here.
    /// </summary>
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

    private static bool HasConcreteProjects(PackageFamilyConfig family)
    {
        return !string.IsNullOrWhiteSpace(family.ManagedProject) && !string.IsNullOrWhiteSpace(family.NativeProject);
    }

    /// <summary>
    /// Guards against scope drift between the manifest-derived smoke scope (see
    /// <see cref="ResolveSmokePackages"/>) and the consumer csprojs that actually carry
    /// the PackageReference entries. If a family graduates to concrete in the manifest,
    /// the runner auto-expands its scope — this check ensures the consumer csprojs were
    /// updated to match, so the smoke surface actually exercises the new package rather
    /// than silently passing while referencing only the old set.
    /// <para>
    /// Fires before any dotnet invocation so the drift surfaces as an actionable error,
    /// not as a green-but-meaningless smoke result.
    /// </para>
    /// </summary>
    private async Task EnsureSmokeCsprojsMatchManifestScopeAsync(IReadOnlyList<SmokePackage> smokePackages, CancellationToken cancellationToken)
    {
        var expectedManagedPackageIds = smokePackages
            .Select(package => package.ManagedPackageId)
            .ToList();

        var consumerProjects = new (FilePath ProjectPath, string Description)[]
        {
            (_pathService.PackageConsumerSmokeProject, "PackageConsumer.Smoke"),
            (_pathService.CompileSanityProject, "Compile.NetStandard"),
        };

        foreach (var (projectPath, description) in consumerProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = _cakeContext.FileSystem.GetFile(projectPath);
            if (!file.Exists)
            {
                throw new CakeException(
                    $"PackageConsumerSmoke cannot find smoke csproj '{projectPath.FullPath}' ({description}).");
            }

            string csprojXml;
            await using (var stream = file.OpenRead())
            using (var reader = new StreamReader(stream))
            {
                csprojXml = await reader.ReadToEndAsync(cancellationToken);
            }

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
                details.Add($"unexpected Janset.SDL.* PackageReference(s) (not concrete in manifest): {string.Join(", ", comparison.Unexpected)}");
            }

            throw new CakeException(
                $"PackageConsumerSmoke scope drift in '{description}' ({projectPath.FullPath}): {string.Join("; ", details)}. " +
                "Update the csproj PackageReferences to match the concrete manifest family set (package_families[] with both managed_project and native_project non-null), or graduate / retire the corresponding family in manifest.json.");
        }
    }

    private void EnsureSelectionSupportsCurrentSmokeScope(IReadOnlyList<SmokePackage> smokePackages)
    {
        if (_packageBuildConfiguration.Families.Count == 0)
        {
            return;
        }

        var selectedFamilies = _packageBuildConfiguration.Families.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingFamilies = smokePackages
            .Where(package => !selectedFamilies.Contains(package.FamilyName))
            .Select(package => package.FamilyName)
            .ToList();

        if (missingFamilies.Count != 0)
        {
            throw new CakeException(
                $"PackageConsumerSmoke currently validates the concrete package-consumer set {DescribeSmokeScope(smokePackages)}. Run with no --family filter, or include the full smoke scope explicitly. Missing: {string.Join(", ", missingFamilies)}. Placeholder families (managed_project or native_project null) are automatically excluded.");
        }
    }

    /// <summary>
    /// Resolves the smoke version and verifies the feed is ready, handling the two valid
    /// version sources:
    /// <list type="number">
    ///   <item><description><c>--family-version</c> explicit (PD-8 manual-escape-hatch):
    ///     pack-existence asserted at exact version, value returned for per-family
    ///     <c>-p:</c> overrides.</description></item>
    ///   <item><description>No flag (default, post-ADR-001 local-dev path): trust
    ///     SetupLocalDev's <c>Janset.Smoke.local.props</c> as the version source of truth.
    ///     The smoke csproj auto-imports it via <c>tests/smoke-tests/Directory.Build.props</c>;
    ///     shared <c>Janset.Smoke.targets</c> expands exact-pin bracket-notation
    ///     <c>PackageReference</c> entries from the per-family version properties. Runner
    ///     only verifies the feed has at least one packed nupkg per concrete family —
    ///     MSBuild + smoke guards (JNSMK001..007) handle the rest at build time.
    ///     </description></item>
    /// </list>
    /// See phase-2-adaptation-plan.md PD-13 for the retirement-review tracking of the
    /// <c>--family-version</c> flag itself.
    /// </summary>
    private string? ResolveSmokeVersionAndEnsureFeed(IReadOnlyList<SmokePackage> smokePackages)
    {
        var version = ResolveOptionalVersion();
        if (version is not null)
        {
            EnsurePackageArtifactsExist(smokePackages, version);
        }
        else
        {
            EnsureLocalPropsFlowIsReady(smokePackages);
        }

        return version;
    }

    private string? ResolveOptionalVersion()
    {
        if (string.IsNullOrWhiteSpace(_packageBuildConfiguration.FamilyVersion))
        {
            return null;
        }

        var result = _packageVersionResolver.Resolve(_packageBuildConfiguration.FamilyVersion);
        if (result.IsError())
        {
            throw new CakeException(
                $"PackageConsumerSmoke received an invalid --family-version. {result.PackageVersionResolutionError.Message}");
        }

        return result.PackageVersion.Value;
    }

    private void EnsurePackageArtifactsExist(IReadOnlyList<SmokePackage> smokePackages, string version)
    {
        foreach (var smokePackage in smokePackages)
        {
            EnsurePackageExists(smokePackage.ManagedPackageId, version);
            EnsurePackageExists(smokePackage.NativePackageId, version);
        }
    }

    private void EnsurePackageExists(string packageId, string version)
    {
        var packagePath = _pathService.PackagesOutput.CombineWithFilePath($"{packageId}.{version}.nupkg");
        if (!_cakeContext.FileExists(packagePath))
        {
            throw new CakeException(
                $"PackageConsumerSmoke expected local feed package '{packagePath.GetFilename().FullPath}' in '{_pathService.PackagesOutput.FullPath}', but it was not found. Run Package first or use a matching --family-version.");
        }
    }

    /// <summary>
    /// Default local-dev smoke path: verify that <see cref="IPathService.GetSmokeLocalPropsFile"/>
    /// exists (SetupLocalDev wrote it) and that the artifact feed contains at least one
    /// <c>{packageId}.*.nupkg</c> per concrete family. We deliberately do not re-implement
    /// per-family version validation here — the shared smoke MSBuild (Janset.Smoke.props +
    /// Janset.Smoke.targets) fires JNSMK001..007 guards at build time if the local.props
    /// values are missing or still carrying the sentinel, so errors surface loud and close
    /// to the operator. Runner's job is just to catch the "you forgot to run SetupLocalDev"
    /// case cleanly, before the smoke cycle spins up.
    /// </summary>
    private void EnsureLocalPropsFlowIsReady(IReadOnlyList<SmokePackage> smokePackages)
    {
        var localPropsFile = _pathService.GetSmokeLocalPropsFile();
        if (!_cakeContext.FileExists(localPropsFile))
        {
            throw new CakeException(
                $"PackageConsumerSmoke expected SetupLocalDev's version-override file at '{localPropsFile.FullPath}' but it is missing. " +
                "Run `SetupLocalDev --source=local --rid <rid>` first to pack the concrete family set and write the override, " +
                "or supply an explicit `--family-version=<semver>` for the PD-8 manual-escape-hatch path.");
        }

        foreach (var smokePackage in smokePackages)
        {
            EnsureFeedHasAtLeastOnePackageFor(smokePackage.ManagedPackageId);
            EnsureFeedHasAtLeastOnePackageFor(smokePackage.NativePackageId);
        }
    }

    private void EnsureFeedHasAtLeastOnePackageFor(string packageId)
    {
        var pattern = _pathService.PackagesOutput.CombineWithFilePath($"{packageId}.*.nupkg").FullPath;
        var matches = _cakeContext.GetFiles(pattern);
        if (matches.Count == 0)
        {
            throw new CakeException(
                $"PackageConsumerSmoke expected at least one '{packageId}.*.nupkg' in '{_pathService.PackagesOutput.FullPath}'. " +
                "Run `SetupLocalDev --source=local --rid <rid>` to populate the local feed, or supply `--family-version` pointing at a pre-packed feed (PD-8).");
        }
    }

    private void RunCompileSanity(FilePath projectPath, IReadOnlyList<SmokePackage> smokePackages, string? version, DirectoryPath packagesCache)
    {
        var arguments = new ProcessArgumentBuilder()
            .Append("build")
            .AppendQuoted(projectPath.FullPath)
            .Append("-c")
            .Append(_dotNetBuildConfiguration.Configuration);

        AppendBuildServerSuppressionFlags(arguments);
        AppendFeedArguments(arguments, packagesCache);
        AppendSmokePackageVersionProperties(arguments, smokePackages, version);

        RunDotNetCommand("compile-sanity netstandard2.0 consumer", arguments);
    }

    private void RunSmokeForTfm(FilePath projectPath, IReadOnlyList<SmokePackage> smokePackages, string? version, DirectoryPath packagesCache, string tfm)
    {
        var arguments = new ProcessArgumentBuilder()
            .Append("test")
            .AppendQuoted(projectPath.FullPath)
            .Append("-c")
            .Append(_dotNetBuildConfiguration.Configuration)
            .Append("-f")
            .Append(tfm)
            .Append("-r")
            .Append(_runtimeProfile.Rid);

        AppendBuildServerSuppressionFlags(arguments);
        AppendFeedArguments(arguments, packagesCache);
        AppendSmokePackageVersionProperties(arguments, smokePackages, version);

        RunDotNetCommand($"test package-smoke ({tfm})", arguments);
    }

    private void AppendFeedArguments(ProcessArgumentBuilder arguments, DirectoryPath packagesCache)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(packagesCache);

        arguments
            .AppendQuoted($"-p:LocalPackageFeed={_pathService.PackagesOutput.FullPath}")
            .AppendQuoted($"-p:RestorePackagesPath={packagesCache.FullPath}");
    }

    /// <summary>
    /// When the operator supplied an explicit <c>--family-version</c> (PD-8 path), forward
    /// it as per-family <c>-p:Janset&lt;Generation&gt;&lt;Role&gt;PackageVersion=&lt;version&gt;</c>
    /// overrides so the same single value is pinned across every family in the smoke scope.
    /// When the flag is absent (default local-dev path), the smoke csproj's auto-import of
    /// <c>Janset.Smoke.local.props</c> provides per-family version properties — runner stays
    /// silent here so it does not override the local.props values with a single shared
    /// version (which would violate D-3seg G54 across families anyway).
    /// </summary>
    private static void AppendSmokePackageVersionProperties(ProcessArgumentBuilder arguments, IReadOnlyList<SmokePackage> smokePackages, string? version)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(smokePackages);

        if (string.IsNullOrEmpty(version))
        {
            return;
        }

        foreach (var smokePackage in smokePackages)
        {
            arguments.Append($"-p:{smokePackage.VersionPropertyName}={version}");
        }
    }

    private static string DescribeSmokeScope(IReadOnlyList<SmokePackage> smokePackages)
    {
        return string.Join(", ", smokePackages.Select(package => $"'{package.FamilyName}'"));
    }

    /// <summary>
    /// Append the trio of flags that keep a single <c>dotnet build/test</c> invocation from
    /// spawning long-lived build-server children:
    /// <list type="bullet">
    ///   <item><description><c>--disable-build-servers</c> — tells the dotnet CLI not to spawn Roslyn / Razor / MSBuild servers, and to shut down any it did end up spawning once the invocation returns.</description></item>
    ///   <item><description><c>-p:UseSharedCompilation=false</c> — defensive for SDKs where the CLI flag is ignored; disables Roslyn VBCSCompiler reuse inside MSBuild.</description></item>
    ///   <item><description><c>-nodeReuse:false</c> — MSBuild's own worker-node reuse flag; otherwise MSBuild keeps a <c>/nodemode:1 /nodeReuse:true</c> process alive ~10 minutes for fast subsequent builds.</description></item>
    /// </list>
    /// Without these, a single PostFlight run on macOS leaves 6–8 dotnet processes
    /// holding ~1 GB of RAM until they time out. On Windows, the same processes hold
    /// file handles on <c>Microsoft.Testing.Platform.dll</c> under the smoke project's
    /// <c>bin/</c> and make the next run's <c>DeleteDirectoryIfExists</c> fail with
    /// <c>UnauthorizedAccessException</c>.
    /// </summary>
    private static void AppendBuildServerSuppressionFlags(ProcessArgumentBuilder arguments)
    {
        arguments
            .Append("--disable-build-servers")
            .Append("-p:UseSharedCompilation=false")
            .Append("-nodeReuse:false");
    }

    /// <summary>
    /// Best-effort <c>dotnet build-server shutdown</c>. Any server that is alive from a
    /// prior build / test run receives a friendly shutdown signal via its named
    /// pipe (Windows) or unix domain socket (Linux/macOS). Failures here are logged at
    /// verbose level and do not abort the run — the subsequent
    /// <see cref="AppendBuildServerSuppressionFlags"/> flags remain the primary defence.
    /// <para>
    /// Side-effect note: the shutdown is per-user, not per-process-tree, so any other
    /// concurrent CLI build on the same machine will re-warm its cache on the next invocation.
    /// See <c>docs/playbook/cross-platform-smoke-validation.md</c> "Lingering dotnet
    /// processes mitigation" — this call fires once on entry and once before each
    /// executable TFM slice (typical total on Windows: 4 shutdowns per PostFlight run).
    /// </para>
    /// </summary>
    private void ShutdownDotNetBuildServers()
    {
        var arguments = new ProcessArgumentBuilder()
            .Append("build-server")
            .Append("shutdown");

        var process = _cakeContext.StartAndReturnProcess(
            "dotnet",
            new ProcessSettings
            {
                Arguments = arguments,
                WorkingDirectory = _pathService.RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Silent = true,
            });

        process.WaitForExit();
        var exitCode = process.GetExitCode();
        if (exitCode != 0)
        {
            _log.Verbose("dotnet build-server shutdown returned exit code {0} (best-effort; run continues).", exitCode);
        }
        else
        {
            _log.Verbose("dotnet build-server shutdown completed.");
        }
    }

    private bool ShouldSkipTfm(string tfm, out string reason)
    {
        reason = string.Empty;

        // Only net4X TFMs need platform-availability gating.
        if (!tfm.StartsWith("net4", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Windows ships .NET Framework natively; TUnit + Microsoft Testing Platform
        // run without any extra runtime installation.
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        if (OperatingSystem.IsMacOS())
        {
            // macOS has no built-in .NET Framework runtime — net462 binaries need
            // classic Mono in PATH (brew install mono / mono-project.com MDK pkg).
            // This also covers the GitHub runner-image regression: macos-14 shipped
            // Mono 6.12, macos-15 (the current macos-latest default) removed it.
            // Detect the mono binary at runtime rather than assuming it; hosts
            // without Mono get a clean skip + reason instead of an MTP
            // "Runner 'mono' not found" stack trace mid-test-run.
            if (IsMonoAvailableOnPath())
            {
                return false;
            }

            reason = $"TFM '{tfm}' runtime execution is skipped on macOS: `mono` binary not found in $PATH. " +
                     "Install classic Mono to enable net462 runtime coverage (see https://www.mono-project.com/download/stable/ or `brew install mono`). " +
                     "Compile-time coverage of net462 still runs via Microsoft.NETFramework.ReferenceAssemblies.";
            return true;
        }

        // Linux path: Mono 6.12 tarball (the latest supported build at time of
        // writing) cannot host TUnit — its source-generated test bootstrap calls
        // System.ValueTuple patterns that Mono does not implement, and the test
        // engine crashes at discovery with
        // MissingMethodException: Method not found: ... TestDataRowUnwrapper.UnwrapArray.
        // The compile-time ref assemblies (Microsoft.NETFramework.ReferenceAssemblies)
        // still exercise the net462 build surface, so runtime gating here does not
        // reduce the coverage the smoke actually provides.
        reason = $"TFM '{tfm}' runtime execution is skipped on Linux: Mono 6.12 cannot host TUnit (MissingMethodException in Microsoft Testing Platform discovery). Compile-time coverage of net462 still runs via Microsoft.NETFramework.ReferenceAssemblies.";
        return true;
    }

    /// <summary>
    /// Probe <c>$PATH</c> for a <c>mono</c> executable using Cake's own environment and
    /// filesystem abstractions — no <c>System.IO</c> calls in the build host by contract.
    /// Used by <see cref="ShouldSkipTfm"/> to decide whether net462 runtime execution can
    /// proceed on non-Windows hosts. File-existence check matches what
    /// <c>Microsoft.Testing.Platform.MSBuild.targets</c> does when it resolves the runner
    /// ("Full path tool calculation" step); when that resolution fails mid-run it surfaces
    /// as an opaque MSBuild error, so detecting the gap up-front keeps the smoke output
    /// readable.
    /// </summary>
    private bool IsMonoAvailableOnPath()
    {
        var pathEnv = _cakeContext.EnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return false;
        }

        // PATH separator is ':' on Unix and ';' on Windows. ShouldSkipTfm already
        // returns false for Windows so in practice this probe only runs on Unix,
        // but branch on the actual host for correctness if the method is ever reused.
        var separator = OperatingSystem.IsWindows() ? ';' : ':';

        foreach (var dir in pathEnv.Split(separator))
        {
            if (string.IsNullOrEmpty(dir))
            {
                continue;
            }

            FilePath candidate;
            try
            {
                candidate = new DirectoryPath(dir).CombineWithFilePath("mono");
            }
            catch (ArgumentException)
            {
                // Malformed PATH entry — ignore and keep probing the rest.
                continue;
            }

            if (_cakeContext.FileExists(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private void RunDotNetCommand(string description, ProcessArgumentBuilder arguments)
    {
        _log.Information("Running dotnet {0}", description);
        _log.Verbose("  dotnet {0}", arguments.Render());

        var process = _cakeContext.StartAndReturnProcess(
            "dotnet",
            new ProcessSettings
            {
                Arguments = arguments,
                WorkingDirectory = _pathService.RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Silent = true,
            });

        process.WaitForExit();

        var standardOutput = process.GetStandardOutput()?.ToList() ?? [];
        var standardError = process.GetStandardError()?.ToList() ?? [];

        foreach (var line in standardOutput)
        {
            _log.Verbose("  [stdout] {0}", line);
        }

        foreach (var line in standardError)
        {
            _log.Verbose("  [stderr] {0}", line);
        }

        var exitCode = process.GetExitCode();
        if (exitCode != 0)
        {
            var combinedOutput = string.Join(Environment.NewLine, standardOutput.Concat(standardError));
            throw new CakeException(
                $"dotnet {description} failed with exit code {exitCode}.{Environment.NewLine}{combinedOutput}");
        }
    }

    private void DeleteDirectoryIfExists(DirectoryPath directoryPath)
    {
        if (_cakeContext.DirectoryExists(directoryPath))
        {
            _cakeContext.DeleteDirectory(directoryPath, new DeleteDirectorySettings { Recursive = true, Force = true });
        }
    }
}
