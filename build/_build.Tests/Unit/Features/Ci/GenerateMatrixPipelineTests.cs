using System.Collections.Immutable;
using Build.Features.Ci;
using Build.Shared.Manifest;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Testing;

namespace Build.Tests.Unit.Features.Ci;

public sealed class GenerateMatrixTaskRunnerTests
{
    [Test]
    public async Task RunAsync_Should_Emit_All_Seven_Runtimes_From_Real_Manifest()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var manifest = ManifestFixture.RealManifest;

        var runner = new GenerateMatrixPipeline(repo.CakeContext, new FakeLog(), repo.Paths, manifest);
        await runner.RunAsync();

        var json = await repo.ReadAllTextAsync("artifacts/matrix/runtimes.json");
        var output = System.Text.Json.JsonSerializer.Deserialize<MatrixOutput>(json);

        await Assert.That(output).IsNotNull();
        await Assert.That(output!.Include.Count).IsEqualTo(manifest.Runtimes.Count);

        var rids = output.Include.Select(e => e.Rid).ToList();
        await Assert.That(rids).Contains("win-x64");
        await Assert.That(rids).Contains("linux-x64");
        await Assert.That(rids).Contains("osx-arm64");
    }

    [Test]
    public async Task RunAsync_Should_Preserve_Triplet_Runner_Strategy_And_Container_Image_Per_Rid()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Unix, repoRoot: "/repo").BuildContextWithHandles();
        var manifest = ManifestFixture.RealManifest;

        var runner = new GenerateMatrixPipeline(repo.CakeContext, new FakeLog(), repo.Paths, manifest);
        await runner.RunAsync();

        var json = await repo.ReadAllTextAsync("artifacts/matrix/runtimes.json");
        var output = System.Text.Json.JsonSerializer.Deserialize<MatrixOutput>(json)!;

        foreach (var seed in manifest.Runtimes)
        {
            var emitted = output.Include.Single(e => string.Equals(e.Rid, seed.Rid, StringComparison.Ordinal));
            await Assert.That(emitted.Triplet).IsEqualTo(seed.Triplet);
            await Assert.That(emitted.Runner).IsEqualTo(seed.Runner);
            await Assert.That(emitted.Strategy).IsEqualTo(seed.Strategy);
            await Assert.That(emitted.ContainerImage).IsEqualTo(seed.ContainerImage);
        }
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Manifest_Runtimes_Is_Empty()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var manifest = ManifestFixture.CreateTestManifestConfig() with
        {
            Runtimes = ImmutableList<RuntimeInfo>.Empty,
        };

        var runner = new GenerateMatrixPipeline(repo.CakeContext, new FakeLog(), repo.Paths, manifest);

        var ex = await Assert.That(() => runner.RunAsync()).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("manifest.runtimes[] is empty");
    }
}
