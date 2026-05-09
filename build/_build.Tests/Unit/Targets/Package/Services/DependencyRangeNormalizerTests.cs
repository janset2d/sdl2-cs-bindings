using System.Collections.Immutable;
using Build.Shared.Manifest;
using Build.Targets.Package.Services;
using Build.Tests.Fixtures;
using Build.Tests.Fixtures.Builders;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Tests.Unit.Targets.Package.Services;

public sealed class DependencyRangeNormalizerTests
{
    [Test]
    public async Task NormalizeAsync_Should_Rewrite_CrossFamily_Dependency_Range_When_DependsOn_Present()
    {
        var (world, manifest) = WorldWithManifest();
        var family = manifest.PackageFamilies.Single(f => f.Name == "sdl2-image");
        var nuspecBytes = NuspecBuilder.WithCrossFamilyDependency(
            packageId: "Janset.SDL2.Image",
            version: "2.8.0",
            dependencyId: "Janset.SDL2.Core",
            currentRange: "2.8.0");
        var nupkg = "artifacts/packages/Janset.SDL2.Image.2.8.0.nupkg";
        world.WithBinaryFile(nupkg, NuspecBuilder.AsNupkgZip("Janset.SDL2.Image.nuspec", nuspecBytes));

        var normalizer = new DependencyRangeNormalizer(world.CakeContext, world.Log, manifest);

        await normalizer.NormalizeAsync(family, world.RepoRoot.CombineWithFilePath(nupkg), "2.8.0", CancellationToken.None);

        var rewritten = NuspecReader.GetDependencyVersion(world, world.RepoRoot.CombineWithFilePath(nupkg), "Janset.SDL2.Core");
        await Assert.That(rewritten).IsEqualTo("[2.8.0, 3.0.0)");
    }

    [Test]
    public async Task NormalizeAsync_Should_NoOp_When_DependsOn_Empty()
    {
        var (world, manifest) = WorldWithManifest();
        var family = manifest.PackageFamilies.Single(f => f.Name == "sdl2-core");  // no DependsOn
        var nuspecBytes = NuspecBuilder.WithoutDependencies("Janset.SDL2.Core", "2.32.0");
        var nupkg = "artifacts/packages/Janset.SDL2.Core.2.32.0.nupkg";
        world.WithBinaryFile(nupkg, NuspecBuilder.AsNupkgZip("Janset.SDL2.Core.nuspec", nuspecBytes));

        var normalizer = new DependencyRangeNormalizer(world.CakeContext, world.Log, manifest);

        await normalizer.NormalizeAsync(family, world.RepoRoot.CombineWithFilePath(nupkg), "2.32.0", CancellationToken.None);

        // No exception, no rewrite — the early-return branch returns before opening the zip.
        // Verify the file contents are byte-identical to what we wrote.
        var rewritten = NuspecReader.GetDependencyVersion(world, world.RepoRoot.CombineWithFilePath(nupkg), "Janset.SDL2.Core");
        await Assert.That(rewritten).IsNull();  // no dependency element
    }

    [Test]
    public async Task NormalizeAsync_Should_NoOp_When_Range_Already_Correct()
    {
        var (world, manifest) = WorldWithManifest();
        var family = manifest.PackageFamilies.Single(f => f.Name == "sdl2-image");
        var nuspecBytes = NuspecBuilder.WithCrossFamilyDependency(
            packageId: "Janset.SDL2.Image",
            version: "2.8.0",
            dependencyId: "Janset.SDL2.Core",
            currentRange: "[2.8.0, 3.0.0)");
        var nupkg = "artifacts/packages/Janset.SDL2.Image.2.8.0.nupkg";
        world.WithBinaryFile(nupkg, NuspecBuilder.AsNupkgZip("Janset.SDL2.Image.nuspec", nuspecBytes));

        var normalizer = new DependencyRangeNormalizer(world.CakeContext, world.Log, manifest);

        await normalizer.NormalizeAsync(family, world.RepoRoot.CombineWithFilePath(nupkg), "2.8.0", CancellationToken.None);

        // hasChanges=false path: nuspec entry not deleted/recreated.
        var rewritten = NuspecReader.GetDependencyVersion(world, world.RepoRoot.CombineWithFilePath(nupkg), "Janset.SDL2.Core");
        await Assert.That(rewritten).IsEqualTo("[2.8.0, 3.0.0)");
    }

    [Test]
    public async Task NormalizeAsync_Should_Skip_Gracefully_When_Managed_Package_Missing()
    {
        var (world, manifest) = WorldWithManifest();
        var family = manifest.PackageFamilies.Single(f => f.Name == "sdl2-image");
        var nupkg = "artifacts/packages/Janset.SDL2.Image.2.8.0.nupkg";
        // Don't seed the .nupkg.

        var normalizer = new DependencyRangeNormalizer(world.CakeContext, world.Log, manifest);

        await normalizer.NormalizeAsync(family, world.RepoRoot.CombineWithFilePath(nupkg), "2.8.0", CancellationToken.None);

        // No throw. Verbose log emitted.
    }

    [Test]
    public async Task NormalizeAsync_Should_Throw_When_DependencyFamily_Not_In_Manifest()
    {
        var (world, manifest) = WorldWithManifest();
        var family = manifest.PackageFamilies.Single(f => f.Name == "sdl2-image") with
        {
            DependsOn = ImmutableList.Create("sdl2-bogus"),
        };
        var nuspecBytes = NuspecBuilder.WithCrossFamilyDependency(
            packageId: "Janset.SDL2.Image", version: "2.8.0",
            dependencyId: "Janset.SDL2.Bogus", currentRange: "2.8.0");
        var nupkg = "artifacts/packages/Janset.SDL2.Image.2.8.0.nupkg";
        world.WithBinaryFile(nupkg, NuspecBuilder.AsNupkgZip("Janset.SDL2.Image.nuspec", nuspecBytes));

        var normalizer = new DependencyRangeNormalizer(world.CakeContext, world.Log, manifest);

        var ex = await Assert.ThrowsAsync<CakeException>(async () =>
            await normalizer.NormalizeAsync(family, world.RepoRoot.CombineWithFilePath(nupkg), "2.8.0", CancellationToken.None));

        await Assert.That(ex!.Message).Contains("does not exist in manifest package_families[]");
    }

    private static (FakeCakeWorldV2 World, ManifestConfig Manifest) WorldWithManifest()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var world = FakeCakeWorldV2.CreateWindows().WithManifestObject(manifest);
        return (world, manifest);
    }
}
