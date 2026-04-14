using System.Text.Json;
using Build.Context.Models;

namespace Build.Tests.Characterization.ConfigContract;

public class ManifestDeserializationTests
{
    private static readonly string ManifestPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "manifest.json");

    [Test]
    public async Task DeserializeManifest_Should_Have_Schema_Version()
    {
        var json = await File.ReadAllTextAsync(ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json);

        await Assert.That(config).IsNotNull();
        await Assert.That(config!.SchemaVersion).IsEqualTo("2.0");
    }

    [Test]
    public async Task DeserializeManifest_Should_Parse_All_Library_Entries()
    {
        var json = await File.ReadAllTextAsync(ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json);

        await Assert.That(config).IsNotNull();
        await Assert.That(config!.LibraryManifests.Count).IsGreaterThanOrEqualTo(6);
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_Exactly_One_Core_Library()
    {
        var json = await File.ReadAllTextAsync(ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        var coreLibs = config.LibraryManifests.Where(m => m.IsCoreLib).ToList();
        await Assert.That(coreLibs.Count).IsEqualTo(1);
        await Assert.That(coreLibs[0].VcpkgName).IsEqualTo("sdl2");
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_Valid_Versions_For_All_Libraries()
    {
        var json = await File.ReadAllTextAsync(ManifestPath).ConfigureAwait(false);
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
        var json = await File.ReadAllTextAsync(ManifestPath).ConfigureAwait(false);
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
        var json = await File.ReadAllTextAsync(ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        await Assert.That(config.PackagingConfig).IsNotNull();
        await Assert.That(config.PackagingConfig!.ValidationMode).IsEqualTo("strict");
        await Assert.That(config.PackagingConfig.CoreLibrary).IsEqualTo("sdl2");
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_Inline_Runtimes()
    {
        var json = await File.ReadAllTextAsync(ManifestPath).ConfigureAwait(false);
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
    public async Task DeserializeManifest_Should_Have_Strategy_Per_Runtime()
    {
        var json = await File.ReadAllTextAsync(ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        foreach (var runtime in config.Runtimes!)
        {
            await Assert.That(runtime.Strategy).IsNotNull();
            await Assert.That(runtime.Strategy == "hybrid-static" || runtime.Strategy == "pure-dynamic").IsTrue();
        }
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_Inline_SystemExclusions()
    {
        var json = await File.ReadAllTextAsync(ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        await Assert.That(config.SystemExclusions).IsNotNull();
        await Assert.That(config.SystemExclusions!.Windows.SystemDlls.Count).IsGreaterThan(0);
        await Assert.That(config.SystemExclusions.Linux.SystemLibraries.Count).IsGreaterThan(0);
        await Assert.That(config.SystemExclusions.Osx.SystemLibraries.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task DeserializeManifest_Should_Have_Hybrid_Triplets_Matching_Strategy()
    {
        var json = await File.ReadAllTextAsync(ManifestPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<ManifestConfig>(json)!;

        foreach (var runtime in config.Runtimes!)
        {
            var isHybridTriplet = runtime.Triplet.Contains("hybrid", StringComparison.OrdinalIgnoreCase);
            var isHybridStrategy = string.Equals(runtime.Strategy, "hybrid-static", StringComparison.OrdinalIgnoreCase);

            await Assert.That(isHybridTriplet).IsEqualTo(isHybridStrategy);
        }
    }
}
