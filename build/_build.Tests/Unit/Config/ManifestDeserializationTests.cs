using System.Text.Json;
using Build.Context.Models;

namespace Build.Tests.Unit.Config;

public class ManifestDeserializationTests
{
    private static readonly string ManifestPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "manifest.json");

    private static readonly string RuntimesPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "runtimes.json");

    private static readonly string SystemArtefactsPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "system_artefacts.json");

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
    public async Task DeserializeRuntimes_Should_Parse_All_Seven_Rids()
    {
        var json = await File.ReadAllTextAsync(RuntimesPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<RuntimeConfig>(json);

        await Assert.That(config).IsNotNull();
        await Assert.That(config!.Runtimes.Count).IsEqualTo(7);
    }

    [Test]
    public async Task DeserializeRuntimes_Should_Have_Valid_Triplet_Per_Rid()
    {
        var json = await File.ReadAllTextAsync(RuntimesPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<RuntimeConfig>(json)!;

        foreach (var runtime in config.Runtimes)
        {
            await Assert.That(runtime.Rid).IsNotNull();
            await Assert.That(runtime.Triplet).IsNotNull();
            await Assert.That(runtime.Runner).IsNotNull();
        }
    }

    [Test]
    public async Task DeserializeSystemArtefacts_Should_Have_Entries_For_All_Platforms()
    {
        var json = await File.ReadAllTextAsync(SystemArtefactsPath).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<SystemArtefactsConfig>(json);

        await Assert.That(config).IsNotNull();
        await Assert.That(config!.Windows.SystemDlls.Count).IsGreaterThan(0);
        await Assert.That(config.Linux.SystemLibraries.Count).IsGreaterThan(0);
        await Assert.That(config.Osx.SystemLibraries.Count).IsGreaterThan(0);
    }
}
