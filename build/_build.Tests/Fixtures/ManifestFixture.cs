using System.Collections.Immutable;
using System.Text.Json;
using Build.Data.BindingGeneration.Models;
using Build.Data.Manifest;
using Build.Data.Manifest.Models;

namespace Build.Tests.Fixtures;

public static class ManifestFixture
{
    private static readonly Lazy<ManifestConfig> CachedManifest = new(LoadManifestFromJson);

    /// <summary>
    /// Full manifest contract fixture captured from production data.
    /// </summary>
    public static ManifestConfig RealManifest => CachedManifest.Value;

    /// <summary>
    /// Core library entry from real manifest.
    /// </summary>
    public static LibraryManifest RealCoreLibrary =>
        RealManifest.LibraryManifests.Single(m => m.IsCoreLib);

    /// <summary>
    /// First satellite library from real manifest.
    /// </summary>
    public static LibraryManifest RealSatelliteLibrary =>
        RealManifest.LibraryManifests.First(m => !m.IsCoreLib);

    /// <summary>
    /// Minimal core library for unit tests that need controlled data.
    /// Use when you need specific values that don't depend on production config.
    /// </summary>
    public static LibraryManifest CreateTestCoreLibrary(string name = "SDL2", string vcpkgName = "sdl2") => new()
    {
        Name = name,
        VcpkgName = vcpkgName,
        VcpkgVersion = "2.32.10",
        VcpkgPortVersion = 0,
        NativeLibName = "SDL2.Core.Native",
        IsCoreLib = true,
        PrimaryBinaries =
        [
            new PrimaryBinary { Os = "Windows", Patterns = ["SDL2.dll"] },
            new PrimaryBinary { Os = "Linux", Patterns = ["libSDL2*"] },
            new PrimaryBinary { Os = "OSX", Patterns = ["libSDL2*.dylib"] },
        ],
        BindingGeneration = CreateTestCoreBindingGeneration(),
    };

    /// <summary>
    /// Minimal satellite library for unit tests that need controlled data.
    /// </summary>
    public static LibraryManifest CreateTestSatelliteLibrary(
        string name = "SDL2_image",
        string vcpkgName = "sdl2-image") => new()
        {
            Name = name,
            VcpkgName = vcpkgName,
            VcpkgVersion = "2.8.8",
            VcpkgPortVersion = 2,
            NativeLibName = "SDL2.Image.Native",
            IsCoreLib = false,
            PrimaryBinaries =
        [
            new PrimaryBinary { Os = "Windows", Patterns = ["SDL2_image.dll"] },
            new PrimaryBinary { Os = "Linux", Patterns = ["libSDL2_image*"] },
            new PrimaryBinary { Os = "OSX", Patterns = ["libSDL2_image*.dylib"] },
        ],
            BindingGeneration = CreateTestSatellitePlaceholderBindingGeneration(
                managedNamespace: "SDL2.Image",
                primaryClassName: "SDL_image",
                platformCatalog: "sdl2-image",
                ownedPrefix: "IMG_"),
        };

    /// <summary>
    /// Minimal core <see cref="BindingGenerationConfig"/> with enabled=true and the
    /// real SDL2.Core identity (matches build/manifest.json's binding_generation block
    /// shape). Used by <see cref="CreateTestCoreLibrary"/> to satisfy the required
    /// <see cref="LibraryManifest.BindingGeneration"/> field.
    /// </summary>
    public static BindingGenerationConfig CreateTestCoreBindingGeneration() => new()
    {
        Enabled = true,
        ManagedNamespace = "SDL2",
        PrimaryClassName = "SDL",
        PlatformCatalogId = "sdl2-core",
        OwnedPrefixes = ["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"],
        ParseDefines = ["SDL_DECLSPEC="],
        ClangArgs = ["-fdeclspec", "-U__has_builtin"],
        ExcludedFunctions = ["SDL_main", "SDL_DYNAPI_entry"],
        HeaderSet = new HeaderSetConfig
        {
            IncludeDirGlob = "include/SDL2",
            HeaderGlob = "*.h",
            ExcludedHeaders = ["SDL.h"],
            ExcludedHeaderPrefixes = ["SDL_test", "SDL2_"],
        },
        Validators = ImmutableDictionary<string, bool>.Empty
            .Add("dynapi-coherence", true)
            .Add("neutral-view-non-empty", true)
            .Add("required-functions-emitted", true)
            .Add("semantic-type-consistency", true),
        Dynapi = new DynapiConfig { ExportsGlob = "buildtrees/sdl2/src/*/src/dynapi/SDL2.exports" },
    };

    /// <summary>
    /// Stage-2-placeholder <see cref="BindingGenerationConfig"/> with enabled=false.
    /// Matches the satellite shape that's in build/manifest.json today — identity
    /// fields populated, generation knobs absent. Used to satisfy
    /// <see cref="LibraryManifest.BindingGeneration"/> for satellite fixtures.
    /// </summary>
    public static BindingGenerationConfig CreateTestSatellitePlaceholderBindingGeneration(
        string managedNamespace,
        string primaryClassName,
        string platformCatalog,
        string ownedPrefix) => new()
    {
        Enabled = false,
        ManagedNamespace = managedNamespace,
        PrimaryClassName = primaryClassName,
        PlatformCatalogId = platformCatalog,
        OwnedPrefixes = [ownedPrefix],
    };

    /// <summary>
    /// Minimal ManifestConfig for unit tests — core + one satellite.
    /// </summary>
    public static ManifestConfig CreateTestManifestConfig() => new()
    {
        SchemaVersion = "2.2",
        PackagingConfig = new PackagingConfig
        {
            ValidationMode = ValidationMode.Strict,
            CoreLibrary = "sdl2",
        },
        Runtimes =
        [
            new RuntimeInfo
            {
                Rid = "win-x64",
                Triplet = "x64-windows-hybrid",
                Runner = "windows-latest",
                ContainerImage = null,
            },
        ],
        PackageFamilies =
        [
            new PackageFamilyConfig
            {
                Name = "sdl2-core",
                TagPrefix = "sdl2-core",
                ManagedProject = "src/SDL2.Core/SDL2.Core.csproj",
                NativeProject = "src/native/SDL2.Core.Native/SDL2.Core.Native.csproj",
                LibraryRef = "SDL2",
                DependsOn = [],
                ChangePaths = ["src/SDL2.Core/**", "src/native/SDL2.Core.Native/**"],
            },
            new PackageFamilyConfig
            {
                Name = "sdl2-image",
                TagPrefix = "sdl2-image",
                ManagedProject = "src/SDL2.Image/SDL2.Image.csproj",
                NativeProject = "src/native/SDL2.Image.Native/SDL2.Image.Native.csproj",
                LibraryRef = "SDL2_image",
                DependsOn = ["sdl2-core"],
                ChangePaths = ["src/SDL2.Image/**", "src/native/SDL2.Image.Native/**"],
            },
        ],
        SystemExclusions = new SystemArtefactsConfig
        {
            Windows = new WindowsSystemArtefacts(),
            Linux = new LinuxSystemArtefacts(),
            Osx = new OsxSystemArtefacts(),
        },
        LibraryManifests = ImmutableList.Create(
            CreateTestCoreLibrary(),
            CreateTestSatelliteLibrary()),
    };

    private static ManifestConfig LoadManifestFromJson()
    {
        var json = FixtureLoader.Load("Manifest/manifest-real.json");
        return JsonSerializer.Deserialize<ManifestConfig>(json)
            ?? throw new InvalidOperationException("Failed to deserialize embedded real manifest fixture.");
    }
}
