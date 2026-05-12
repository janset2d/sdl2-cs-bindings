using System.Collections.Immutable;
using System.Text.Json;
using Build.Data;
using Build.Data.Manifest.Models;
using Build.Targets.GenerateMatrix;
using Build.Targets.GenerateMatrix.Models;
using Build.Tests.Fixtures;

namespace Build.Tests.Scenarios.GenerateMatrix;

/// <summary>
/// In-process behavior coverage for <c>GenerateMatrixTask</c>: exercises real task
/// orchestration against the fake Cake world and asserts the emitted
/// <c>artifacts/matrix/runtimes.json</c> shape against the seeded manifest.
/// </summary>
public sealed class GenerateMatrixTaskScenarioTests
{
    [Test]
    public async Task RunAsync_Should_Emit_All_Seven_Runtimes_From_Real_Manifest()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.RealManifest);

        var run = await CreateHost(world).WithManifest(ManifestFixture.RealManifest).RunAsync();

        await Assert.That(run.Success).IsTrue().Because(run.Exception?.ToString() ?? "no exception");

        var json = world.ReadAllText("artifacts/matrix/runtimes.json");
        var output = JsonSerializer.Deserialize<MatrixOutput>(json);

        await Assert.That(output).IsNotNull();
        await Assert.That(output!.Include.Count).IsEqualTo(ManifestFixture.RealManifest.Runtimes.Count);

        var rids = output.Include.Select(e => e.Rid).ToList();
        await Assert.That(rids).Contains("win-x64");
        await Assert.That(rids).Contains("linux-x64");
        await Assert.That(rids).Contains("osx-arm64");
    }

    [Test]
    public async Task RunAsync_Should_Preserve_Triplet_Runner_And_Container_Image_Per_Rid()
    {
        var world = FakeCakeWorldV2.CreateLinux()
            .WithManifestObject(ManifestFixture.RealManifest);

        var run = await CreateHost(world).WithManifest(ManifestFixture.RealManifest).RunAsync();

        await Assert.That(run.Success).IsTrue().Because(run.Exception?.ToString() ?? "no exception");

        var json = world.ReadAllText("artifacts/matrix/runtimes.json");
        var output = JsonSerializer.Deserialize<MatrixOutput>(json)!;

        foreach (var seed in ManifestFixture.RealManifest.Runtimes)
        {
            var emitted = output.Include.Single(e => string.Equals(e.Rid, seed.Rid, StringComparison.Ordinal));
            await Assert.That(emitted.Triplet).IsEqualTo(seed.Triplet);
            await Assert.That(emitted.Runner).IsEqualTo(seed.Runner);
            await Assert.That(emitted.ContainerImage).IsEqualTo(seed.ContainerImage);
        }
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Manifest_Runtimes_Is_Empty()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var manifest = ManifestFixture.CreateTestManifestConfig() with
        {
            Runtimes = ImmutableList<RuntimeInfo>.Empty,
        };

        var run = await CreateHost(world).WithManifest(manifest).RunAsync();

        await Assert.That(run.Success).IsFalse();
        await Assert.That(run.Exception).IsNotNull();
        await Assert.That(run.Exception!.Message).Contains("manifest.runtimes[] is empty");
    }

    private static TargetTestHostV2<GenerateMatrixTask> CreateHost(FakeCakeWorldV2 world)
    {
        return new TargetTestHostV2<GenerateMatrixTask>(world)
            .WithServices(services => services.AddData());
    }
}
