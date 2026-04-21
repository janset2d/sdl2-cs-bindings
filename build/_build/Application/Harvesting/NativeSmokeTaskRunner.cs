using Build.Context;
using Build.Context.Models;
using Build.Domain.Harvesting.Models;
using Build.Domain.Paths;
using Build.Domain.Runtime;
using Build.Infrastructure.Tools.NativeSmoke;
using Cake.CMake;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Build.Application.Harvesting;

/// <summary>
/// Runs the native C smoke harness after harvest to validate RID payload usability.
/// </summary>
public sealed class NativeSmokeTaskRunner(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    IRuntimeProfile runtimeProfile,
    ManifestConfig manifestConfig,
    IMsvcDevEnvironment msvcDevEnvironment)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IMsvcDevEnvironment _msvcDevEnvironment = msvcDevEnvironment ?? throw new ArgumentNullException(nameof(msvcDevEnvironment));

    public async Task RunAsync(BuildContext context, NativeSmokeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureNativeSmokeInputsReady();

        var libraries = ResolveLibrariesToValidate(context);
        EnsureHarvestPayloadReady(context, libraries);

        var preset = request.Rid;
        await RunCmakeConfigureAsync(preset);
        cancellationToken.ThrowIfCancellationRequested();
        await RunCmakeBuildAsync(preset);
        cancellationToken.ThrowIfCancellationRequested();
        RunNativeSmokeBinary(preset);

        _log.Information("NativeSmoke completed successfully for RID '{0}'.", request.Rid);
    }

    private void EnsureNativeSmokeInputsReady()
    {
        var projectDir = _pathService.NativeSmokeProjectDir;
        if (!_cakeContext.DirectoryExists(projectDir))
        {
            throw new CakeException(
                $"NativeSmoke precondition failed: project directory '{projectDir.FullPath}' is missing. " +
                "Sync the repository checkout before running the native smoke stage.");
        }

        var cmakeListsFile = projectDir.CombineWithFilePath("CMakeLists.txt");
        if (!_cakeContext.FileExists(cmakeListsFile))
        {
            throw new CakeException(
                $"NativeSmoke precondition failed: '{cmakeListsFile.FullPath}' is missing. " +
                "Sync the repository checkout before running the native smoke stage.");
        }

        var cmakePresetsFile = projectDir.CombineWithFilePath("CMakePresets.json");
        if (!_cakeContext.FileExists(cmakePresetsFile))
        {
            throw new CakeException(
                $"NativeSmoke precondition failed: '{cmakePresetsFile.FullPath}' is missing. " +
                "Sync the repository checkout before running the native smoke stage.");
        }
    }

    private List<LibraryManifest> ResolveLibrariesToValidate(BuildContext context)
    {
        var specifiedLibraries = context.Vcpkg.Libraries;
        var manifestLibraries = _manifestConfig.LibraryManifests.ToList();

        if (!specifiedLibraries.Any())
        {
            return manifestLibraries;
        }

        var result = new List<LibraryManifest>(specifiedLibraries.Count);
        foreach (var specified in specifiedLibraries)
        {
            var manifest = manifestLibraries.SingleOrDefault(m =>
                string.Equals(m.Name, specified, StringComparison.OrdinalIgnoreCase));

            if (manifest is null)
            {
                throw new CakeException($"Library '{specified}' was requested via --library but is missing in manifest.json.");
            }

            result.Add(manifest);
        }

        return result;
    }

    private void EnsureHarvestPayloadReady(BuildContext context, IReadOnlyList<LibraryManifest> libraries)
    {
        foreach (var libraryName in libraries.Select(l => l.Name))
        {
            var nativeDir = _pathService
                .GetHarvestLibraryRidRuntimesDir(libraryName, _runtimeProfile.Rid)
                .Combine("native");

            if (!context.DirectoryExists(nativeDir))
            {
                throw new CakeException(
                    $"NativeSmoke precondition failed: '{nativeDir.FullPath}' is missing for library '{libraryName}'. " +
                    $"Run '--target Harvest --rid {_runtimeProfile.Rid}' first.");
            }

            var hasPayload = context.GetFiles($"{nativeDir.FullPath}/**/*").Count > 0;
            if (!hasPayload)
            {
                throw new CakeException(
                    $"NativeSmoke precondition failed: '{nativeDir.FullPath}' is empty for library '{libraryName}'. " +
                    $"Run '--target Harvest --rid {_runtimeProfile.Rid}' first.");
            }
        }
    }

    private async Task RunCmakeConfigureAsync(string preset)
    {
        _log.Information("NativeSmoke configure: cmake --preset {0}", preset);

        var settings = new CMakeSettings
        {
            SourcePath = _pathService.NativeSmokeProjectDir,
            Options = ["--preset", preset],
        };

        await ApplyMsvcEnvironmentAsync(settings);
        _cakeContext.CMake(settings);
    }

    private async Task RunCmakeBuildAsync(string preset)
    {
        // Cake.CMake's CMakeBuildRunner emits `cmake --build <BinaryPath>` unconditionally
        // (BinaryPath is required and validated). Targeting the preset's configured binary
        // directory directly is equivalent to `cmake --build --preset <preset>` once configure
        // has already populated the cache (which RunCmakeConfigureAsync guarantees).
        // CMakePresets v3 uses `binaryDir: "${sourceDir}/build/${presetName}"`, which matches
        // GetNativeSmokeBuildPresetDir().
        var binaryPath = _pathService.GetNativeSmokeBuildPresetDir(preset);
        _log.Information("NativeSmoke build: cmake --build {0}", binaryPath.FullPath);

        var buildSettings = new CMakeBuildSettings
        {
            BinaryPath = binaryPath,
        };

        await ApplyMsvcEnvironmentAsync(buildSettings);
        _cakeContext.CMakeBuild(buildSettings);
    }

    /// <summary>
    /// Merges the <see cref="IMsvcDevEnvironment"/> delta into a Cake
    /// <see cref="ToolSettings.EnvironmentVariables"/> dictionary so the Ninja + <c>cl.exe</c>
    /// child process inherits a live MSVC toolchain without the operator having to spawn
    /// Cake from a Developer PowerShell. Two early returns surface the no-op cases
    /// explicitly:
    /// <list type="bullet">
    ///   <item><description>Non-Windows host — gcc / clang are expected on <c>$PATH</c>,
    ///     MSVC injection is irrelevant. Caller-side gate keeps the resolver's
    ///     Windows-only contract (see <see cref="IMsvcDevEnvironment"/> docs).</description></item>
    ///   <item><description>Windows host with MSVC already sourced in the parent
    ///     shell (<c>VCToolsInstallDir</c> set). Resolver returns an empty delta;
    ///     nothing to merge.</description></item>
    /// </list>
    /// Initialises <see cref="ToolSettings.EnvironmentVariables"/> on demand —
    /// Cake defaults it to <see langword="null"/>.
    /// </summary>
    private async Task ApplyMsvcEnvironmentAsync(ToolSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var delta = await _msvcDevEnvironment.ResolveAsync();
        if (delta.Count == 0)
        {
            return;
        }

        settings.EnvironmentVariables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in delta)
        {
            settings.EnvironmentVariables[entry.Key] = entry.Value;
        }
    }

    private void RunNativeSmokeBinary(string preset)
    {
        var executable = _pathService.GetNativeSmokeExecutableFile(preset);
        if (!_cakeContext.FileExists(executable))
        {
            throw new CakeException(
                $"NativeSmoke executable not found after build: '{executable.FullPath}'.");
        }

        var result = _cakeContext.NativeSmokeRun(new NativeSmokeRunnerSettings(executable));

        foreach (var line in result.StandardOutput)
        {
            _log.Information("[native-smoke] {0}", line);
        }

        foreach (var line in result.StandardError)
        {
            _log.Warning("[native-smoke:stderr] {0}", line);
        }

        if (result.ExitCode != 0)
        {
            throw new CakeException(
                $"NativeSmoke failed with exit code {result.ExitCode} for RID '{_runtimeProfile.Rid}'.");
        }
    }
}
