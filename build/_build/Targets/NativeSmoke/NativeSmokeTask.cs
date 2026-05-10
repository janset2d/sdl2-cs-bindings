using Build.Host;
using Build.Host.Paths;
using Build.Data.Manifest;
using Build.Runtime;
using Build.Targets.NativeSmoke.Requests;
using Build.Targets.NativeSmoke.Services;
using Build.Tools.NativeSmoke;
using Build.Validation.NativeSmoke;
using Cake.CMake;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.Tooling;
using Cake.Frosting;

namespace Build.Targets.NativeSmoke;

/// <summary>
/// Cake target that runs the native C smoke harness against the harvested per-RID payload to
/// prove the runtime closure is loadable + the satellite ABI matches expectations. Owns
/// orchestration directly: rid + cohort precondition validation, library-set resolution,
/// per-library harvest-payload readiness check, then cmake configure → build → smoke-binary
/// execute. Configure + Build merge the MSVC env delta on Windows (cl.exe + Ninja inheritance);
/// Execute lets the binary inherit the parent shell's PATH. The MSVC probe is triggered on
/// the first ConfigureAsync call and cached per-arch by <see cref="IMsvcDevEnvironment"/>, so
/// any toolchain failure surfaces before cmake.exe is invoked. Direct successor to the retired
/// pre-migration NativeSmokePipeline.
/// </summary>
[TaskName("NativeSmoke")]
[TaskDescription("Runs native C smoke harness against harvested runtime payload")]
public sealed class NativeSmokeTask(
    INativeSmokePreconditionsValidator preconditions,
    IMsvcDevEnvironment msvcDevEnvironment,
    ICakeContext cakeContext,
    IPathService pathService,
    IRuntimeProfile runtimeProfile,
    ManifestConfig manifestConfig) : AsyncFrostingTask<BuildContext>
{
    private readonly INativeSmokePreconditionsValidator _preconditions = preconditions ?? throw new ArgumentNullException(nameof(preconditions));
    private readonly IMsvcDevEnvironment _msvcDevEnvironment = msvcDevEnvironment ?? throw new ArgumentNullException(nameof(msvcDevEnvironment));
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.Runtime.Rid))
        {
            throw new CakeException("NativeSmoke requires --rid <rid>. Example: --target NativeSmoke --rid win-x64");
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

        var libraries = ResolveLibrariesToValidate(context.Libraries);
        EnsureHarvestPayloadReady(context, libraries);

        var request = new NativeSmokeRequest(context.Runtime.Rid);

        // Configure → Build → Execute: MSVC probe (Windows-only) is triggered inside
        // ConfigureAsync's ApplyMsvcEnvironmentAsync before _cakeContext.CMake(settings)
        // runs, so a toolchain failure surfaces as CakeException before cmake.exe is
        // invoked. Subsequent BuildAsync hits the per-arch cache.
        await ConfigureAsync(request.Rid).ConfigureAwait(false);
        await BuildAsync(request.Rid).ConfigureAwait(false);
        Execute(request.Rid);

        context.Log.Information("NativeSmoke completed successfully for RID '{0}'.", request.Rid);
    }

    private async Task ConfigureAsync(string rid)
    {
        _cakeContext.Log.Information("NativeSmoke configure: cmake --preset {0}", rid);

        var settings = new CMakeSettings
        {
            SourcePath = _pathService.NativeSmokeProjectDir,
            Options = ["--preset", rid],
        };

        await ApplyMsvcEnvironmentAsync(settings, rid).ConfigureAwait(false);
        _cakeContext.CMake(settings);
    }

    private async Task BuildAsync(string rid)
    {
        // Cake.CMake's CMakeBuildRunner emits `cmake --build <BinaryPath>` unconditionally
        // (BinaryPath is required and validated). Targeting the preset's configured binary
        // directory is equivalent to `cmake --build --preset <rid>` once configure has
        // populated the cache. CMakePresets v3 maps `binaryDir: "${sourceDir}/build/${presetName}"`
        // to GetNativeSmokeBuildPresetDir().
        var binaryPath = _pathService.GetNativeSmokeBuildPresetDir(rid);
        _cakeContext.Log.Information("NativeSmoke build: cmake --build {0}", binaryPath.FullPath);

        var settings = new CMakeBuildSettings
        {
            BinaryPath = binaryPath,
        };

        await ApplyMsvcEnvironmentAsync(settings, rid).ConfigureAwait(false);
        _cakeContext.CMakeBuild(settings);
    }

    private void Execute(string rid)
    {
        var executable = _pathService.GetNativeSmokeExecutableFile(rid);
        if (!_cakeContext.FileExists(executable))
        {
            throw new CakeException($"NativeSmoke executable not found after build: '{executable.FullPath}'.");
        }

        var result = _cakeContext.NativeSmokeRun(new NativeSmokeRunnerSettings(executable));

        foreach (var line in result.StandardOutput)
        {
            _cakeContext.Log.Information("[native-smoke] {0}", line);
        }

        foreach (var line in result.StandardError)
        {
            _cakeContext.Log.Warning("[native-smoke:stderr] {0}", line);
        }

        if (result.ExitCode != 0)
        {
            throw new CakeException($"NativeSmoke failed with exit code {result.ExitCode} for RID '{rid}'.");
        }
    }

    /// <summary>
    /// Merges the <see cref="IMsvcDevEnvironment"/> delta into a Cake
    /// <see cref="ToolSettings.EnvironmentVariables"/> dictionary so the Ninja + cl.exe
    /// child process inherits a live MSVC toolchain for the rid's target architecture,
    /// without the operator having to spawn Cake from a Developer PowerShell. Two early
    /// returns surface no-op cases: non-Windows host (gcc/clang on $PATH carry the build),
    /// and Windows with parent shell already MSVC-sourced (resolver returns empty delta).
    /// Wraps <see cref="MsvcTargetArchExtensions.FromRid"/>'s PlatformNotSupportedException
    /// so an unsupported RID surfaces as a CakeException with operator-actionable context.
    /// </summary>
    private async Task ApplyMsvcEnvironmentAsync(ToolSettings settings, string rid)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        MsvcTargetArch targetArch;
        try
        {
            targetArch = MsvcTargetArchExtensions.FromRid(rid);
        }
        catch (PlatformNotSupportedException ex)
        {
            throw new CakeException($"NativeSmoke prereq: {ex.Message}", ex);
        }

        var delta = await _msvcDevEnvironment.ResolveAsync(targetArch).ConfigureAwait(false);
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

    private List<LibraryManifest> ResolveLibrariesToValidate(IReadOnlyList<string> requestedLibraries)
    {
        var allManifestLibraries = _manifestConfig.LibraryManifests.ToList();

        if (requestedLibraries.Count == 0)
        {
            return allManifestLibraries;
        }

        var librariesToValidate = new List<LibraryManifest>(requestedLibraries.Count);
        foreach (var specLibName in requestedLibraries)
        {
            var manifest = allManifestLibraries.SingleOrDefault(m => string.Equals(m.Name, specLibName, StringComparison.OrdinalIgnoreCase))
                ?? throw new CakeException($"Library '{specLibName}' was requested via --library but is missing in manifest.json.");

            librariesToValidate.Add(manifest);
        }

        return librariesToValidate;
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
}
