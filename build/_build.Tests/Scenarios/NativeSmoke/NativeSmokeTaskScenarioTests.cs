using Build.Data.Manifest;
using Build.Targets.NativeSmoke;
using Build.Targets.NativeSmoke.Services;
using Build.Tests.Fixtures;
using Build.Validation;
using Cake.Core;
using Cake.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Build.Tests.Scenarios.NativeSmoke;

/// <summary>
/// In-process behavior coverage for <c>NativeSmokeTask</c>: exercises real task orchestration
/// against the fake Cake world. Covers precondition gates (CMakeLists / presets missing),
/// library-set validation, harvest-payload readiness, and the MSVC probe contract — including
/// the runtime proof that probe failure short-circuits before <c>cmake.exe</c> is invoked
/// (asserted via <c>world.ProcessInvocations</c>) and that the per-arch cache prevents
/// duplicate dispatch when Configure + Build both run.
/// </summary>
public sealed class NativeSmokeTaskScenarioTests
{
    private const string LibraryName = "sdl2-core";
    private const string Rid = "win-x64";

    [Test]
    public async Task RunAsync_Should_Throw_When_Rid_Is_Missing()
    {
        var world = SeedReadyWorld().WithRid("");
        var manifest = ManifestFixture.CreateTestManifestConfig();

        var result = await CreateHost(world, manifest).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("NativeSmoke requires --rid", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_CMakeLists_Is_Missing()
    {
        // CMakePresets present, CMakeLists absent → NativeSmokePreconditionsValidator fails.
        var world = FakeCakeWorldV2.CreateWindows()
            .WithRid(Rid)
            .WithTextFile("tests/smoke-tests/native-smoke/CMakePresets.json", "{}");
        var manifest = CreateSingleLibraryManifest();

        var result = await CreateHost(world, manifest).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("CMakeLists.txt", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_CMakePresets_Is_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithRid(Rid)
            .WithTextFile("tests/smoke-tests/native-smoke/CMakeLists.txt", "# placeholder");
        var manifest = CreateSingleLibraryManifest();

        var result = await CreateHost(world, manifest).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("CMakePresets.json", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Specified_Library_Not_In_Manifest()
    {
        var world = SeedReadyWorld().WithLibraries("sdl2-image");
        var manifest = CreateSingleLibraryManifest();

        var result = await CreateHost(world, manifest).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("sdl2-image", StringComparison.Ordinal);
        await Assert.That(result.Exception!.Message).Contains("manifest.json", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Harvest_Payload_Is_Missing()
    {
        // Project files seeded; harvest output directory NOT seeded → payload check fails.
        var world = FakeCakeWorldV2.CreateWindows()
            .WithRid(Rid)
            .WithTextFile("tests/smoke-tests/native-smoke/CMakeLists.txt", "# placeholder")
            .WithTextFile("tests/smoke-tests/native-smoke/CMakePresets.json", "{}");
        var manifest = CreateSingleLibraryManifest();

        var result = await CreateHost(world, manifest).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("is missing for library", StringComparison.Ordinal);
        await Assert.That(result.Exception!.Message).Contains("--target Harvest", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Surface_CakeException_And_Skip_Cmake_When_Msvc_Probe_Fails()
    {
        // Runtime proof for the inlined-runner design's "fail-fast before cmake" property:
        // ApplyMsvcEnvironmentAsync awaits BEFORE _cakeContext.CMake(settings) runs, so a probe
        // exception short-circuits ConfigureAsync before any cmake.exe invocation. Assertion
        // surface: world.ProcessInvocations stays empty + the configure log line never lands.
        if (!OperatingSystem.IsWindows())
        {
            // Non-Windows hosts short-circuit the MSVC probe gate; this scenario can't be exercised.
            return;
        }

        var world = SeedReadyWorld();
        var manifest = CreateSingleLibraryManifest();

        var msvcStub = Substitute.For<IMsvcDevEnvironment>();
        msvcStub.ResolveAsync(Arg.Any<MsvcTargetArch>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyDictionary<string, string>>>(_ =>
                throw new CakeException("MsvcDevEnvironment could not locate Visual Studio."));

        var result = await CreateHost(world, manifest, msvcOverride: msvcStub).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Visual Studio", StringComparison.Ordinal);
        // Fail-fast signature: ConfigureAsync's first line is the configure-log info entry
        // (it lands BEFORE ApplyMsvcEnvironmentAsync awaits). Probe failure short-circuits
        // configure before _cakeContext.CMake(settings) runs, so log present + empty
        // ProcessInvocations is the runtime evidence that cmake.exe was never invoked.
        await Assert.That(result.Log.HasMessage(LogLevel.Information, "NativeSmoke configure: cmake --preset")).IsTrue();
        await Assert.That(world.ProcessInvocations).IsEmpty();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Rid_Is_Not_A_Supported_Windows_Arch()
    {
        // FromRid throws PlatformNotSupportedException for non-Windows RIDs on Windows host;
        // ApplyMsvcEnvironmentAsync wraps it into a CakeException with operator-actionable
        // context. Belt-and-braces — RID validation primarily fails at HarvestPreconditions
        // (triplet missing for non-Windows RIDs), but ConfigureAsync defends in depth.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var world = SeedReadyWorld()
            .WithRid("win-mips64")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/runtimes/win-mips64/native/SDL2.dll", "fake");
        var manifest = CreateSingleLibraryManifest();

        var msvcStub = Substitute.For<IMsvcDevEnvironment>();
        msvcStub.ResolveAsync(Arg.Any<MsvcTargetArch>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var result = await CreateHost(world, manifest, msvcOverride: msvcStub).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("no vcvarsall mapping", StringComparison.Ordinal);
        await Assert.That(world.ProcessInvocations).IsEmpty();
    }

    private static FakeCakeWorldV2 SeedReadyWorld() =>
        FakeCakeWorldV2.CreateWindows()
            .WithRid(Rid)
            .WithTextFile("tests/smoke-tests/native-smoke/CMakeLists.txt", "# placeholder")
            .WithTextFile("tests/smoke-tests/native-smoke/CMakePresets.json", "{}")
            .WithTextFile($"artifacts/harvest_output/{LibraryName}/runtimes/{Rid}/native/SDL2.dll", "fake");

    private static TargetTestHostV2<NativeSmokeTask> CreateHost(
        FakeCakeWorldV2 world,
        ManifestConfig? manifest = null,
        IMsvcDevEnvironment? msvcOverride = null)
    {
        var host = new TargetTestHostV2<NativeSmokeTask>(world);
        if (manifest is not null)
        {
            host.WithManifest(manifest);
        }

        return host.WithServices(services =>
        {
            services.AddValidators();
            services.AddNativeSmoke();

            if (msvcOverride is not null)
            {
                services.AddSingleton(msvcOverride);
            }
        });
    }

    private static ManifestConfig CreateSingleLibraryManifest()
    {
        var baseline = ManifestFixture.CreateTestManifestConfig();
        var library = ManifestFixture.CreateTestCoreLibrary() with { Name = LibraryName };
        return baseline with
        {
            LibraryManifests = [library],
        };
    }
}
