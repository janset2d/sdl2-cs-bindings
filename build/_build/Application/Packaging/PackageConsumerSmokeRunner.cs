using System.Diagnostics.CodeAnalysis;
using Build.Context;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Packaging;
using Build.Domain.Packaging.Models;
using Build.Domain.Paths;
using Build.Domain.Preflight;
using Build.Domain.Runtime;
using Build.Infrastructure.DotNet;
using Cake.Common;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Application.Packaging;

public sealed class PackageConsumerSmokeRunner(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    ManifestConfig manifestConfig,
    DotNetBuildConfiguration dotNetBuildConfiguration,
    IProjectMetadataReader projectMetadataReader,
    IDotNetRuntimeEnvironment dotNetRuntimeEnvironment) : IPackageConsumerSmokeRunner
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly DotNetBuildConfiguration _dotNetBuildConfiguration = dotNetBuildConfiguration ?? throw new ArgumentNullException(nameof(dotNetBuildConfiguration));
    private readonly IProjectMetadataReader _projectMetadataReader = projectMetadataReader ?? throw new ArgumentNullException(nameof(projectMetadataReader));
    private readonly IDotNetRuntimeEnvironment _dotNetRuntimeEnvironment = dotNetRuntimeEnvironment ?? throw new ArgumentNullException(nameof(dotNetRuntimeEnvironment));

    /// <summary>
    /// Concrete family descriptor for smoke: derived from <c>manifest.json</c> package_families
    /// filtered by presence of managed + native project (same <c>HasConcreteProjects</c> rule
    /// pack flow uses when it resolves the explicit version mapping). Naming conventions come from
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

    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "Linear orchestration: guards + feed resolve + compile-sanity + per-TFM smoke. Splitting hurts traceability of the operator-visible pass/fail sequence.")]
    public async Task RunAsync(BuildContext context, PackageConsumerSmokeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // Require versions on the request itself. This runner does not fall back to
        // Janset.Local.props, which keeps package version injection on a single path.
        if (request.Versions.Count == 0)
        {
            throw new CakeException(
                "PackageConsumerSmoke requires a non-empty version mapping on the request. " +
                "Invoke the target with repeated --explicit-version family=semver entries " +
                "(or rely on PackageConsumerSmokeTask.ShouldRun to skip silently when the " +
                "operator did not supply any).");
        }

        EnsureSmokeStageInputsReady(request.FeedPath);

        var smokePackages = ResolveSmokePackages();
        EnsureSelectionSupportsCurrentSmokeScope(smokePackages, request.Versions);
        await EnsureSmokeCsprojsMatchManifestScopeAsync(smokePackages, cancellationToken);

        var explicitVersions = ResolveSmokeVersionMappingAndEnsureFeed(smokePackages, request.Versions, request.FeedPath);

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

        // 1. Resolve executable TFMs up-front. The same list drives both per-TFM smoke and
        //    any RID-specific child-runtime bootstrap (today: x86 hostfxr/runtime injection
        //    for win-x86 apphosts on Windows CI).
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

        // 1b. Compile-only sanity for the netstandard2.0 consumer slice.
        //     netstandard2.0 is a contract, not a runtime — if this library compiles
        //     against our package, the netstandard2.0 consumer surface is validated.
        RunCompileSanity(compileSanityProject, smokePackages, explicitVersions, packagesCache, request.FeedPath);

        var runtimeEnvironmentDelta = await _dotNetRuntimeEnvironment.ResolveAsync(
            request.Rid,
            metadataResult.ProjectMetadata.TargetFrameworks,
            cancellationToken);

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

            RunSmokeForTfm(
                smokeProject,
                smokePackages,
                explicitVersions,
                packagesCache,
                request.FeedPath,
                request.Rid,
                tfm,
                runtimeEnvironmentDelta);
        }
    }

    private void EnsureSmokeStageInputsReady(DirectoryPath feedPath)
    {
        if (!_cakeContext.FileExists(_pathService.PackageConsumerSmokeProject))
        {
            throw new CakeException(
                $"PackageConsumerSmoke precondition failed: smoke project '{_pathService.PackageConsumerSmokeProject.FullPath}' is missing. " +
                "Sync the repository checkout before running the consumer smoke stage.");
        }

        if (!_cakeContext.FileExists(_pathService.CompileSanityProject))
        {
            throw new CakeException(
                $"PackageConsumerSmoke precondition failed: compile-sanity project '{_pathService.CompileSanityProject.FullPath}' is missing. " +
                "Sync the repository checkout before running the consumer smoke stage.");
        }

        if (!_cakeContext.DirectoryExists(feedPath))
        {
            throw new CakeException(
                $"PackageConsumerSmoke precondition failed: local feed directory '{feedPath.FullPath}' is missing. " +
                "Run 'SetupLocalDev --source=local --rid <rid>' or '--target Package --explicit-version <family>=<semver>' first.");
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

    private static void EnsureSelectionSupportsCurrentSmokeScope(IReadOnlyList<SmokePackage> smokePackages, IReadOnlyDictionary<string, NuGetVersion> explicitVersions)
    {
        if (explicitVersions.Count == 0)
        {
            return;
        }

        var missingFamilies = smokePackages
            .Where(package => !explicitVersions.ContainsKey(package.FamilyName))
            .Select(package => package.FamilyName)
            .ToList();

        if (missingFamilies.Count != 0)
        {
            throw new CakeException(
                $"PackageConsumerSmoke currently validates the concrete package-consumer set {DescribeSmokeScope(smokePackages)}. Run without --explicit-version to use the SetupLocalDev-written local.props default, or include the full smoke scope in --explicit-version. Missing: {string.Join(", ", missingFamilies)}. Placeholder families (managed_project or native_project null) are automatically excluded.");
        }
    }

    /// <summary>
    /// Resolves the smoke version mapping and verifies the feed is ready. Post-C.8 the runner
    /// only accepts a non-empty <c>--explicit-version</c> mapping; the legacy empty-mapping →
    /// props-fallback path retired when Deniz Q5a direction landed (2026-04-21).
    /// Per-family pack-existence is asserted at this gate so a missing nupkg surfaces with
    /// the SetupLocalDev remediation hint rather than opaquely inside <c>dotnet restore</c>.
    /// </summary>
    private IReadOnlyDictionary<string, NuGetVersion> ResolveSmokeVersionMappingAndEnsureFeed(
        IReadOnlyList<SmokePackage> smokePackages,
        IReadOnlyDictionary<string, NuGetVersion> explicitVersions,
        DirectoryPath feedPath)
    {
        EnsurePackageArtifactsExist(smokePackages, explicitVersions, feedPath);
        return explicitVersions;
    }

    private void EnsurePackageArtifactsExist(IReadOnlyList<SmokePackage> smokePackages, IReadOnlyDictionary<string, NuGetVersion> explicitVersions, DirectoryPath feedPath)
    {
        foreach (var smokePackage in smokePackages)
        {
            var version = explicitVersions[smokePackage.FamilyName].ToNormalizedString();
            EnsurePackageExists(smokePackage.ManagedPackageId, version, feedPath);
            EnsurePackageExists(smokePackage.NativePackageId, version, feedPath);
        }
    }

    private void EnsurePackageExists(string packageId, string version, DirectoryPath feedPath)
    {
        var packagePath = feedPath.CombineWithFilePath($"{packageId}.{version}.nupkg");
        if (!_cakeContext.FileExists(packagePath))
        {
            throw new CakeException(
                $"PackageConsumerSmoke expected local feed package '{packagePath.GetFilename().FullPath}' in '{feedPath.FullPath}', but it was not found. Run Package first or use a matching --explicit-version entry.");
        }
    }

    private void RunCompileSanity(FilePath projectPath, IReadOnlyList<SmokePackage> smokePackages, IReadOnlyDictionary<string, NuGetVersion> explicitVersions, DirectoryPath packagesCache, DirectoryPath feedPath)
    {
        var arguments = new ProcessArgumentBuilder()
            .Append("build")
            .AppendQuoted(projectPath.FullPath)
            .Append("-c")
            .Append(_dotNetBuildConfiguration.Configuration);

        AppendBuildServerSuppressionFlags(arguments);
        AppendFeedArguments(arguments, packagesCache, feedPath);
        AppendSmokePackageVersionProperties(arguments, smokePackages, explicitVersions);

        RunDotNetCommand("compile-sanity netstandard2.0 consumer", arguments, echoStdout: true);
    }

    private void RunSmokeForTfm(
        FilePath projectPath,
        IReadOnlyList<SmokePackage> smokePackages,
        IReadOnlyDictionary<string, NuGetVersion> explicitVersions,
        DirectoryPath packagesCache,
        DirectoryPath feedPath,
        string rid,
        string tfm,
        IReadOnlyDictionary<string, string> dotNetRuntimeEnvironment)
    {
        var arguments = new ProcessArgumentBuilder()
            .Append("test")
            .AppendQuoted(projectPath.FullPath)
            .Append("-c")
            .Append(_dotNetBuildConfiguration.Configuration)
            .Append("-f")
            .Append(tfm)
            .Append("-r")
            .Append(rid);

        // .NET Framework + AnyCPU + native package presence triggers SDK's auto-x86
        // RuntimeIdentifierInference. Our consumer-side .targets resolves the copy RID
        // from $(Platform) (not $(RuntimeIdentifier)) precisely because user-explicit `-r`
        // and SDK auto-x86 produce indistinguishable property states. Forwarding `-p:Platform=<arch>`
        // alongside `-r <rid>` makes the smoke runner's intent explicit and lets the
        // .targets pick up the correct native DLL set. Only applies on Windows + net4x;
        // other TFMs/RIDs use the standard SDK runtimes/<rid>/native flow.
        AppendNet4xPlatformArgument(arguments, rid, tfm);

        AppendBuildServerSuppressionFlags(arguments);
        AppendFeedArguments(arguments, packagesCache, feedPath);
        AppendSmokePackageVersionProperties(arguments, smokePackages, explicitVersions);

        RunDotNetCommand(
            $"test package-smoke ({tfm})",
            arguments,
            echoStdout: true,
            environmentVariables: dotNetRuntimeEnvironment);
    }

    /// <summary>
    /// Map a Windows RID to the matching MSBuild Platform value when the smoke target
    /// framework is .NET Framework. Disambiguates user-explicit RID from SDK's auto-x86
    /// inference inside the consumer-side native DLL copy target. No-op for non-Windows
    /// RIDs and non-.NET Framework TFMs.
    /// </summary>
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
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(packagesCache);
        ArgumentNullException.ThrowIfNull(feedPath);

        arguments
            .AppendQuoted($"-p:LocalPackageFeed={feedPath.FullPath}")
            .AppendQuoted($"-p:RestorePackagesPath={packagesCache.FullPath}");
    }

    /// <summary>
    /// Forward each <c>--explicit-version family=semver</c> entry as a per-family
    /// <c>-p:Janset&lt;Generation&gt;&lt;Role&gt;PackageVersion=&lt;version&gt;</c> override
    /// so the consumer csproj restores the exact-matching nupkg set. Mapping is mandatory
    /// post-C.8 (<see cref="PackageConsumerSmokeRequest"/> guarantees non-empty by runner
    /// entry-point guard).
    /// </summary>
    private static void AppendSmokePackageVersionProperties(
        ProcessArgumentBuilder arguments,
        IReadOnlyList<SmokePackage> smokePackages,
        IReadOnlyDictionary<string, NuGetVersion> explicitVersions)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(smokePackages);
        ArgumentNullException.ThrowIfNull(explicitVersions);

        foreach (var smokePackage in smokePackages)
        {
            if (explicitVersions.TryGetValue(smokePackage.FamilyName, out var version))
            {
                arguments.Append($"-p:{smokePackage.VersionPropertyName}={version.ToNormalizedString()}");
            }
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
    /// Without these, a single PackageConsumerSmoke run on macOS leaves 6–8 dotnet processes
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
    /// executable TFM slice (typical total on Windows: 4 shutdowns per PackageConsumerSmoke run).
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

    /// <summary>
    /// Spawns <c>dotnet</c> with the supplied arguments, captures stdout/stderr, and
    /// echoes them at a caller-chosen verbosity. Test invocations set
    /// <paramref name="echoStdout"/> to <see langword="true"/> so TUnit's per-TFM pass/fail
    /// summary surfaces at the normal Cake log level; build-server-shutdown and similar
    /// chatty-but-uninteresting commands leave it <see langword="false"/> to keep the
    /// smoke log readable. Failure always surfaces the combined output in the thrown
    /// <see cref="CakeException"/>.
    /// </summary>
    private void RunDotNetCommand(
        string description,
        ProcessArgumentBuilder arguments,
        bool echoStdout = false,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        _log.Information("Running dotnet {0}", description);
        _log.Verbose("  dotnet {0}", arguments.Render());

        var process = _cakeContext.StartAndReturnProcess(
            "dotnet",
            new ProcessSettings
            {
                Arguments = arguments,
                EnvironmentVariables = environmentVariables is null || environmentVariables.Count == 0
                    ? null
                    : new Dictionary<string, string>(environmentVariables, StringComparer.OrdinalIgnoreCase),
                WorkingDirectory = _pathService.RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Silent = true,
            });

        process.WaitForExit();

        var standardOutput = process.GetStandardOutput()?.ToList() ?? [];
        var standardError = process.GetStandardError()?.ToList() ?? [];

        var stdoutLevel = echoStdout ? LogLevel.Information : LogLevel.Verbose;
        var stderrLevel = echoStdout ? LogLevel.Warning : LogLevel.Verbose;

        foreach (var line in standardOutput)
        {
            _log.Write(Verbosity.Normal, stdoutLevel, "  [stdout] {0}", line);
        }

        foreach (var line in standardError)
        {
            _log.Write(Verbosity.Normal, stderrLevel, "  [stderr] {0}", line);
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
