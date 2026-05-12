using System.Collections.Immutable;
using Build.Data;
using Build.Data.Manifest.Models;
using Build.Targets.PreFlightCheck;
using Build.Tests.Fixtures;
using Build.Validation;
using Cake.Core.Diagnostics;

namespace Build.Tests.Scenarios.PreFlightCheck;

/// <summary>
/// In-process behavior coverage for <c>PreFlightCheckTask</c>: exercises real task
/// orchestration against the fake Cake world, asserts that each of the seven
/// cross-cutting validators surfaces its expected failure mode, and that the
/// boundary preconditions (versions-file argument, non-empty version mapping)
/// fail loudly before any validator runs. The happy-path scenario seeds the full
/// manifest+vcpkg+overlay+csproj surface so every validator returns clean.
/// </summary>
public sealed class PreFlightCheckTaskScenarioTests
{
    private const string VersionsFilePath = "artifacts/resolve-versions/versions.json";

    [Test]
    public async Task RunAsync_Should_Pass_When_All_Validators_Are_Green()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(manifest)
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
            .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# overlay")
            .WithVersionsFile(VersionsFilePath)
            .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-valid.json"));

        SeedCsprojsForManifestFixture(world);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Exception).IsNull();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_VersionsFile_Argument_Is_Missing()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
            .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# overlay");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("--versions-file");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Version_Mapping_Is_Empty()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
            .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# overlay")
            .WithVersionsFile(VersionsFilePath)
            .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-empty.json"));

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("non-empty version mapping");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_HybridStatic_Overlay_File_Is_Missing()
    {
        // [G16] — manifest declares triplet x64-windows-hybrid but no matching overlay file
        // exists at vcpkg-overlay-triplets/. PreFlight must refuse before any build runs.
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
            .WithVersionsFile(VersionsFilePath)
            .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-valid.json"));

        SeedCsprojsForManifestFixture(world);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Pre-flight check failed");
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "[G16]")).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Vcpkg_Override_Drifts_From_Manifest()
    {
        // VersionConsistency — vcpkg.json carries sdl2 v2.30.0 while manifest library_manifests
        // pins sdl2 to v2.32.10. PreFlight catches the drift before downstream packing
        // would silently consume the wrong upstream.
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-version-mismatch.json"))
            .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# overlay")
            .WithVersionsFile(VersionsFilePath)
            .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-valid.json"));

        SeedCsprojsForManifestFixture(world);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Pre-flight check failed");
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "version inconsistencies")).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Manifest_Family_Name_Has_Uppercase()
    {
        // [G59] — a manifest entry with a hand-edited mixed-case name slips past JSON
        // schema and breaks PackageFamilyId ordinal-exact lookups downstream. PreFlight
        // catches it before the Pack stage misroutes versions.
        var manifestWithBadName = ManifestFixture.CreateTestManifestConfig() with
        {
            PackageFamilies = ImmutableList.Create(
                new PackageFamilyConfig
                {
                    Name = "SDL2-Bad",
                    TagPrefix = "sdl2-bad",
                    ManagedProject = null,
                    NativeProject = null,
                    LibraryRef = "SDL2",
                    DependsOn = [],
                    ChangePaths = [],
                }),
        };
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(manifestWithBadName)
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
            .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# overlay")
            .WithVersionsFile(VersionsFilePath)
            .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-single-family.json"));

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Pre-flight check failed");
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "[G59]")).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Upstream_Version_Major_Differs()
    {
        // [G54] — versions.json carries sdl2-core="3.0.0" while manifest library_manifests
        // pins sdl2 to upstream 2.32.10. Major drift breaks D-3seg version contract.
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
            .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# overlay")
            .WithVersionsFile(VersionsFilePath)
            .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-upstream-mismatch.json"));

        SeedCsprojsForManifestFixture(world);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Pre-flight check failed");
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "[G54]")).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_CrossFamily_Dependency_Missing_From_Scope()
    {
        // [G58] — versions.json carries sdl2-image but not sdl2-core; sdl2-image declares
        // depends_on=["sdl2-core"], so the satellite-only release would yield a package
        // that cannot resolve its within-monorepo dependency at restore time.
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
            .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# overlay")
            .WithVersionsFile(VersionsFilePath)
            .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-cross-family-missing.json"));

        SeedCsprojsForManifestFixture(world);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Pre-flight check failed");
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "[G58]")).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Csproj_Files_Are_Missing()
    {
        // CsprojPackContractValidator — manifest references managed/native csproj files
        // that don't exist on disk. Catches stale package_families[] entries pointing at
        // moved or deleted projects.
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
            .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# overlay")
            .WithVersionsFile(VersionsFilePath)
            .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-valid.json"));

        // Intentionally skip SeedCsprojsForManifestFixture — csproj files absent.

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Pre-flight check failed");
        await Assert.That(result.Log.HasMessage(LogLevel.Error, "csproj pack contract")).IsTrue();
    }

    private static TargetTestHost<PreFlightCheckTask> CreateHost(FakeCakeWorld world)
    {
        return new TargetTestHost<PreFlightCheckTask>(world)
            .WithServices(services =>
            {
                services.AddData();
                services.AddValidators();
                services.AddPreFlightCheck();
            });
    }

    private static void SeedCsprojsForManifestFixture(FakeCakeWorld world)
    {
        // Minimal csproj XML that satisfies CsprojPackContractValidator G6 (canonical
        // PackageId) and G7 (Native ProjectReference path). Mirrors the manifest fixture's
        // family→csproj mapping verbatim.
        world.WithTextFile("src/SDL2.Core/SDL2.Core.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Janset.SDL2.Core</PackageId>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\native\SDL2.Core.Native\SDL2.Core.Native.csproj" />
              </ItemGroup>
            </Project>
            """);

        world.WithTextFile("src/native/SDL2.Core.Native/SDL2.Core.Native.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Janset.SDL2.Core.Native</PackageId>
              </PropertyGroup>
            </Project>
            """);

        world.WithTextFile("src/SDL2.Image/SDL2.Image.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Janset.SDL2.Image</PackageId>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\native\SDL2.Image.Native\SDL2.Image.Native.csproj" />
              </ItemGroup>
            </Project>
            """);

        world.WithTextFile("src/native/SDL2.Image.Native/SDL2.Image.Native.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Janset.SDL2.Image.Native</PackageId>
              </PropertyGroup>
            </Project>
            """);
    }
}
