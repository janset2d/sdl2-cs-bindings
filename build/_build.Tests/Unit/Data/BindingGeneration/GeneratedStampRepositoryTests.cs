using Build.Data.BindingGeneration;
using Build.Host.Cake;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Data.BindingGeneration;

public sealed class GeneratedStampRepositoryTests
{
    [Test]
    public async Task SaveAsync_Should_Write_Deterministic_Json_Without_Wall_Clock_Time()
    {
        var world = FakeCakeWorld.CreateLinux();
        var output = world.RepoRoot.Combine("Generated");
        var stampPath = output.CombineWithFilePath(".generated-stamp");
        var stamp = new GeneratedStamp(
            SchemaVersion: 1,
            Family: "sdl2-core",
            GeneratorAssembly: "Build.Targets.GenerateBindings",
            CppAstVersion: "0.24.0",
            LibClangVersion: "20.1.2",
            VcpkgTriplet: "x64-linux-hybrid",
            VcpkgManifestHash: "sha256:vcpkg",
            VcpkgBaseline: "0b88aacde46a853151730fbe7d0b7ee45f4b6864",
            ManifestLibraryVersion: "2.32.10",
            HeaderFingerprint: "sha256:abc",
            HeaderCount: 54,
            ParseViews: ["Neutral", "Windows"]);
        var repository = new GeneratedStampRepository(world.CakeContext);

        await repository.SaveAsync(output, stamp);
        var first = await world.CakeContext.ReadAllTextAsync(stampPath);
        await repository.SaveAsync(output, stamp);
        var second = await world.CakeContext.ReadAllTextAsync(stampPath);
        var loaded = await repository.LoadAsync(output);

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(second).DoesNotContain("generated_at");
        await Assert.That(second).Contains("schema_version");
        await Assert.That(loaded.Family).IsEqualTo(stamp.Family);
        await Assert.That(loaded.HeaderFingerprint).IsEqualTo(stamp.HeaderFingerprint);
        await Assert.That(loaded.ParseViews).IsEquivalentTo(stamp.ParseViews);
    }
}
