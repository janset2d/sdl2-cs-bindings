using System.Text.Json;
using Build.Data.Manifest;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Manifest;

public class RealManifestContractTests
{
    [Test]
    public async Task DeserializeManifest_Should_Parse_All_Library_Entries()
    {
        var json = await WorkspaceFiles.ReadAllTextAsync(WorkspaceFiles.ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json);

        await Assert.That(config).IsNotNull();
        await Assert.That(config!.LibraryManifests.Count).IsGreaterThanOrEqualTo(5);
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_Exactly_One_Core_Library()
    {
        var json = await WorkspaceFiles.ReadAllTextAsync(WorkspaceFiles.ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        var coreLibs = config.LibraryManifests.Where(m => m.IsCoreLib).ToList();
        await Assert.That(coreLibs.Count).IsEqualTo(1);
        await Assert.That(coreLibs[0].VcpkgName).IsEqualTo("sdl2");
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_Valid_Versions_For_All_Libraries()
    {
        var json = await WorkspaceFiles.ReadAllTextAsync(WorkspaceFiles.ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        foreach (var lib in config.LibraryManifests)
        {
            await Assert.That(lib.VcpkgVersion).IsNotNull();
            await Assert.That(lib.VcpkgVersion.Split('.').Length).IsGreaterThanOrEqualTo(3);
            await Assert.That(lib.NativeLibName).IsNotNull();
            await Assert.That(lib.PrimaryBinaries.Count).IsGreaterThanOrEqualTo(1);
        }
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_Platform_Binaries_For_All_Three_OS()
    {
        var json = await WorkspaceFiles.ReadAllTextAsync(WorkspaceFiles.ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        foreach (var lib in config.LibraryManifests)
        {
            var osList = lib.PrimaryBinaries.Select(pb => pb.Os).ToList();
            await Assert.That(osList).Contains("Windows");
            await Assert.That(osList).Contains("Linux");
            await Assert.That(osList).Contains("OSX");
        }
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_PackagingConfig()
    {
        var json = await WorkspaceFiles.ReadAllTextAsync(WorkspaceFiles.ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        await Assert.That(config.PackagingConfig).IsNotNull();
        await Assert.That(config.PackagingConfig.ValidationMode).IsEqualTo(ValidationMode.Strict);
        await Assert.That(config.PackagingConfig.CoreLibrary).IsEqualTo("sdl2");
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_All_Package_Families()
    {
        var json = await WorkspaceFiles.ReadAllTextAsync(WorkspaceFiles.ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        await Assert.That(config.PackageFamilies).IsNotNull();
        await Assert.That(config.PackageFamilies.Count).IsEqualTo(5);

        var familyNames = config.PackageFamilies.Select(f => f.Name).ToList();
        await Assert.That(familyNames).Contains("sdl2-core");
        await Assert.That(familyNames).Contains("sdl2-image");
        await Assert.That(familyNames).Contains("sdl2-mixer");
        await Assert.That(familyNames).Contains("sdl2-ttf");
        await Assert.That(familyNames).Contains("sdl2-gfx");
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_Package_Families_Referencing_Library_Manifests()
    {
        var json = await WorkspaceFiles.ReadAllTextAsync(WorkspaceFiles.ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        var knownLibraries = config.LibraryManifests.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var family in config.PackageFamilies)
        {
            await Assert.That(knownLibraries.Contains(family.LibraryRef)).IsTrue();
        }
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_Inline_Runtimes()
    {
        var json = await WorkspaceFiles.ReadAllTextAsync(WorkspaceFiles.ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        await Assert.That(config.Runtimes).IsNotNull();
        await Assert.That(config.Runtimes!.Count).IsEqualTo(7);

        foreach (var runtime in config.Runtimes)
        {
            await Assert.That(runtime.Rid).IsNotNull();
            await Assert.That(runtime.Triplet).IsNotNull();
            await Assert.That(runtime.Runner).IsNotNull();
        }
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_Inline_SystemExclusions()
    {
        var json = await WorkspaceFiles.ReadAllTextAsync(WorkspaceFiles.ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        await Assert.That(config.SystemExclusions).IsNotNull();
        await Assert.That(config.SystemExclusions!.Windows.SystemDlls.Count).IsGreaterThan(0);
        await Assert.That(config.SystemExclusions.Linux.SystemLibraries.Count).IsGreaterThan(0);
        await Assert.That(config.SystemExclusions.Osx.SystemLibraries.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_Hybrid_Overlay_Triplets_For_All_Runtimes()
    {
        var json = await WorkspaceFiles.ReadAllTextAsync(WorkspaceFiles.ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        foreach (var runtime in config.Runtimes!)
        {
            await Assert.That(runtime.Triplet.EndsWith("-hybrid", StringComparison.OrdinalIgnoreCase))
                .IsTrue()
                .Because($"runtime '{runtime.Rid}' triplet '{runtime.Triplet}' must end with '-hybrid' post-S11 (PreFlight HybridStaticOverlayValidator).");
        }
    }
}
