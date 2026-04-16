using System.Collections.Immutable;
using System.Text.Json;
using Build.Context.Models;
using Build.Modules.Strategy.Models;

namespace Build.Tests.Fixtures;

public static class ManifestFixture
{
    private static readonly Lazy<ManifestConfig> CachedManifest = new(LoadManifestFromJson);

    /// <summary>
    /// Real manifest.json from build/ directory — production data.
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
        NativeLibVersion = "2.32.10.0",
        IsCoreLib = true,
        PrimaryBinaries =
        [
            new PrimaryBinary { Os = "Windows", Patterns = ["SDL2.dll"] },
            new PrimaryBinary { Os = "Linux", Patterns = ["libSDL2*"] },
            new PrimaryBinary { Os = "OSX", Patterns = ["libSDL2*.dylib"] },
        ],
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
        NativeLibVersion = "2.8.8.0",
        IsCoreLib = false,
        PrimaryBinaries =
        [
            new PrimaryBinary { Os = "Windows", Patterns = ["SDL2_image.dll"] },
            new PrimaryBinary { Os = "Linux", Patterns = ["libSDL2_image*"] },
            new PrimaryBinary { Os = "OSX", Patterns = ["libSDL2_image*.dylib"] },
        ],
    };

    /// <summary>
    /// Minimal ManifestConfig for unit tests — core + one satellite.
    /// </summary>
    public static ManifestConfig CreateTestManifestConfig() => new()
    {
        SchemaVersion = "2.1",
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
                Strategy = "hybrid-static",
                Runner = "windows-latest",
                ContainerImage = null,
            },
        ],
        PackageFamilies =
        [
            new PackageFamilyConfig
            {
                Name = "core",
                TagPrefix = "core",
                ManagedProject = "src/SDL2.Core/SDL2.Core.csproj",
                NativeProject = "src/native/SDL2.Core.Native/SDL2.Core.Native.csproj",
                LibraryRef = "SDL2",
                DependsOn = [],
                ChangePaths = ["src/SDL2.Core/**", "src/native/SDL2.Core.Native/**"],
            },
            new PackageFamilyConfig
            {
                Name = "image",
                TagPrefix = "image",
                ManagedProject = "src/SDL2.Image/SDL2.Image.csproj",
                NativeProject = "src/native/SDL2.Image.Native/SDL2.Image.Native.csproj",
                LibraryRef = "SDL2_image",
                DependsOn = ["core"],
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
        var json = WorkspaceFiles.ReadAllText(WorkspaceFiles.ManifestPath);
        return JsonSerializer.Deserialize<ManifestConfig>(json)
            ?? throw new InvalidOperationException("Failed to deserialize manifest.json");
    }
}
