using Build.Infrastructure.Vcpkg;
using Cake.Core.IO;
using Cake.Testing;

namespace Build.Tests.Unit.Infrastructure.Vcpkg;

public sealed class VcpkgManifestReaderTests
{
    private static VcpkgManifestReader CreateReader() => new(new FakeFileSystem(FakeEnvironment.CreateUnixEnvironment()));

    [Test]
    public async Task Parse_Should_Read_Overrides_From_Json()
    {
        const string json = """
            {
              "overrides": [
                { "name": "sdl2", "version": "2.32.10", "port-version": 0 }
              ]
            }
            """;

        var manifest = CreateReader().Parse(json);

        await Assert.That(manifest.Overrides).IsNotNull();
        await Assert.That(manifest.Overrides!.Count).IsEqualTo(1);
        await Assert.That(manifest.Overrides[0].Name).IsEqualTo("sdl2");
    }

    [Test]
    public async Task ParseFile_Should_Read_Content_Through_Cake_FileSystem_Abstraction()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var path = new FilePath("/fake/vcpkg.json");
        fileSystem.CreateFile(path).SetContent("""
            {
              "overrides": [
                { "name": "sdl2-image", "version": "2.8.8", "port-version": 1 }
              ]
            }
            """);

        var reader = new VcpkgManifestReader(fileSystem);
        var manifest = reader.ParseFile(path);

        await Assert.That(manifest.Overrides).IsNotNull();
        await Assert.That(manifest.Overrides![0].Name).IsEqualTo("sdl2-image");
        await Assert.That(manifest.Overrides[0].PortVersion).IsEqualTo(1);
    }
}