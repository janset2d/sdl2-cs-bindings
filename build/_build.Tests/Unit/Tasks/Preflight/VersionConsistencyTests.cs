using Build.Context.Models;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Tasks.Preflight;

public class VersionConsistencyTests
{
    [Test]
    public async Task VcpkgOverride_Should_Deserialize_With_PortVersion()
    {
        const string json = """
                            {
                                "overrides": [
                                    { "name": "sdl2", "version": "2.32.10", "port-version": 0 },
                                    { "name": "sdl2-image", "version": "2.8.8", "port-version": 2 }
                                ]
                            }
                            """;

        var manifest = System.Text.Json.JsonSerializer.Deserialize<VcpkgManifest>(json);

        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest!.Overrides).IsNotNull();
        await Assert.That(manifest.Overrides!.Count).IsEqualTo(2);
        await Assert.That(manifest.Overrides[0].Name).IsEqualTo("sdl2");
        await Assert.That(manifest.Overrides[0].Version).IsEqualTo("2.32.10");
        await Assert.That(manifest.Overrides[0].PortVersion).IsEqualTo(0);
        await Assert.That(manifest.Overrides[1].PortVersion).IsEqualTo(2);
    }

    [Test]
    public async Task VcpkgOverride_Should_Default_PortVersion_To_Null_When_Missing()
    {
        const string json = """
                            {
                                "overrides": [
                                    { "name": "sdl2-gfx", "version": "1.0.4" }
                                ]
                            }
                            """;

        var manifest = System.Text.Json.JsonSerializer.Deserialize<VcpkgManifest>(json);

        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest!.Overrides![0].PortVersion).IsNull();
    }

    [Test]
    public async Task VcpkgManifest_Should_Handle_No_Overrides()
    {
        const string json = """
                            {
                                "dependencies": ["sdl2"]
                            }
                            """;

        var manifest = System.Text.Json.JsonSerializer.Deserialize<VcpkgManifest>(json);

        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest!.Overrides).IsNull();
    }

    [Test]
    public async Task RealVcpkgJson_Should_Deserialize_Successfully()
    {
        if (!WorkspaceFiles.Exists(WorkspaceFiles.VcpkgManifestPath))
        {
            // Skip if vcpkg.json not available (CI without full checkout)
            return;
        }

        var json = await WorkspaceFiles.ReadAllTextAsync(WorkspaceFiles.VcpkgManifestPath).ConfigureAwait(false);
        var manifest = System.Text.Json.JsonSerializer.Deserialize<VcpkgManifest>(json);

        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest!.Overrides).IsNotNull();
        await Assert.That(manifest.Overrides!.Count).IsGreaterThan(0);

        // All overrides should have name and version
        foreach (var o in manifest.Overrides)
        {
            await Assert.That(o.Name).IsNotNull();
            await Assert.That(o.Version).IsNotNull();
        }
    }
}
