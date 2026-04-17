using Build.Context.Configs;
using Build.Modules.Contracts;
using Cake.Common;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Modules.Packaging;

public sealed class PackageConsumerSmokeRunner(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    IRuntimeProfile runtimeProfile,
    DotNetBuildConfiguration dotNetBuildConfiguration,
    PackageBuildConfiguration packageBuildConfiguration,
    IPackageVersionResolver packageVersionResolver,
    IProjectMetadataReader projectMetadataReader) : IPackageConsumerSmokeRunner
{
    private const string CoreFamily = "sdl2-core";
    private const string ImageFamily = "sdl2-image";
    private const string SmokeProjectRelativePath = "tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj";
    private const string CompileSanityProjectRelativePath = "tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj";

    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly DotNetBuildConfiguration _dotNetBuildConfiguration = dotNetBuildConfiguration ?? throw new ArgumentNullException(nameof(dotNetBuildConfiguration));
    private readonly PackageBuildConfiguration _packageBuildConfiguration = packageBuildConfiguration ?? throw new ArgumentNullException(nameof(packageBuildConfiguration));
    private readonly IPackageVersionResolver _packageVersionResolver = packageVersionResolver ?? throw new ArgumentNullException(nameof(packageVersionResolver));
    private readonly IProjectMetadataReader _projectMetadataReader = projectMetadataReader ?? throw new ArgumentNullException(nameof(projectMetadataReader));

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        EnsureSelectionSupportsCurrentSmokeScope();

        var version = ResolveVersion();
        EnsurePackageArtifactsExist(version);

        var smokeProject = _pathService.RepoRoot.CombineWithFilePath(new FilePath(SmokeProjectRelativePath));
        var compileSanityProject = _pathService.RepoRoot.CombineWithFilePath(new FilePath(CompileSanityProjectRelativePath));

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
        RunCompileSanity(compileSanityProject, version, packagesCache);

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

            RunSmokeForTfm(smokeProject, version, packagesCache, tfm);
        }
    }

    private void EnsureSelectionSupportsCurrentSmokeScope()
    {
        if (_packageBuildConfiguration.Families.Count == 0)
        {
            return;
        }

        var selectedFamilies = _packageBuildConfiguration.Families.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!selectedFamilies.Contains(CoreFamily) || !selectedFamilies.Contains(ImageFamily))
        {
            throw new CakeException(
                "PackageConsumerSmoke currently validates the Phase 2a integration pair 'sdl2-core' + 'sdl2-image'. Run with no --family filter, or include both families explicitly.");
        }
    }

    private string ResolveVersion()
    {
        var result = _packageVersionResolver.Resolve(_packageBuildConfiguration.FamilyVersion);
        if (result.IsError())
        {
            // PackageConsumerSmoke has a narrower user-facing contract than Package
            // (it consumes the already-packed feed), so prefix the resolver's message
            // to make the surface explicit in task-level output.
            throw new CakeException(
                $"PackageConsumerSmoke requires a valid --family-version matching the packed feed. {result.PackageVersionResolutionError.Message}");
        }

        return result.PackageVersion.Value;
    }

    private void EnsurePackageArtifactsExist(string version)
    {
        EnsurePackageExists("Janset.SDL2.Core", version);
        EnsurePackageExists("Janset.SDL2.Core.Native", version);
        EnsurePackageExists("Janset.SDL2.Image", version);
        EnsurePackageExists("Janset.SDL2.Image.Native", version);
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

    private void RunCompileSanity(FilePath projectPath, string version, DirectoryPath packagesCache)
    {
        var arguments = new ProcessArgumentBuilder()
            .Append("build")
            .AppendQuoted(projectPath.FullPath)
            .Append("-c")
            .Append(_dotNetBuildConfiguration.Configuration)
            .Append($"-p:JansetSdl2CorePackageVersion={version}")
            .Append($"-p:JansetSdl2ImagePackageVersion={version}")
            .AppendQuoted($"-p:LocalPackageFeed={_pathService.PackagesOutput.FullPath}")
            .AppendQuoted($"-p:RestorePackagesPath={packagesCache.FullPath}");

        RunDotNetCommand("compile-sanity netstandard2.0 consumer", arguments);
    }

    private void RunSmokeForTfm(FilePath projectPath, string version, DirectoryPath packagesCache, string tfm)
    {
        var arguments = new ProcessArgumentBuilder()
            .Append("test")
            .AppendQuoted(projectPath.FullPath)
            .Append("-c")
            .Append(_dotNetBuildConfiguration.Configuration)
            .Append("-f")
            .Append(tfm)
            .Append("-r")
            .Append(_runtimeProfile.Rid)
            .Append($"-p:JansetSdl2CorePackageVersion={version}")
            .Append($"-p:JansetSdl2ImagePackageVersion={version}")
            .AppendQuoted($"-p:LocalPackageFeed={_pathService.PackagesOutput.FullPath}")
            .AppendQuoted($"-p:RestorePackagesPath={packagesCache.FullPath}");

        RunDotNetCommand($"test package-smoke ({tfm})", arguments);
    }

    private bool ShouldSkipTfm(string tfm, out string reason)
    {
        reason = string.Empty;

        // Only net4X TFMs have runtime-availability concerns outside Windows.
        if (!tfm.StartsWith("net4", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Windows ships .NET Framework natively.
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        // Unix: .NET Framework execution requires Mono (WineHQ-maintained since Aug 2024).
        // Build-time reference assemblies came from Microsoft.NETFramework.ReferenceAssemblies,
        // so the compile was fine regardless of Mono availability.
        if (IsMonoAvailable())
        {
            return false;
        }

        reason = $"TFM '{tfm}' execution requires Mono on Unix; 'mono' not found in PATH. Install 'mono-complete' (Linux) or via Homebrew (macOS) to enable this TFM slice of the smoke.";
        return true;
    }

    private bool IsMonoAvailable()
    {
        // Process.Start throws Win32Exception when the executable can't be located
        // (e.g., 'mono' not in PATH). That is the "not available" signal we're probing for;
        // any other exception is unexpected and should propagate.
        try
        {
            var process = _cakeContext.StartAndReturnProcess(
                "mono",
                new ProcessSettings
                {
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Silent = true,
                });

            process.WaitForExit();
            return process.GetExitCode() == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
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
