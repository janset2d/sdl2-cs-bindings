using System.Collections.Immutable;
using Build.Data.Manifest;
using Build.Tests.Fixtures;
using Build.Validation.Packaging;
using Cake.Core;

namespace Build.Tests.Unit.Validation.Packaging;

public sealed class HarvestReadinessValidatorTests
{
    private const string LibraryRef = "sdl2";
    private const string FamilyName = "sdl2-core";

    [Test]
    public async Task EnsureReadyAsync_Should_Return_When_All_Gates_Pass()
    {
        var world = ReadyWorld();

        var validator = NewValidator(world);

        await validator.EnsureReadyAsync(TestFamily(), CancellationToken.None);
        // No throw == pass.
    }

    [Test]
    public async Task EnsureReadyAsync_Should_Throw_When_Harvest_Manifest_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var validator = NewValidator(world);

        var ex = await Assert.ThrowsAsync<CakeException>(async () =>
            await validator.EnsureReadyAsync(TestFamily(), CancellationToken.None));

        await Assert.That(ex!.Message).Contains("harvest manifest");
        await Assert.That(ex!.Message).Contains("is missing");
    }

    [Test]
    public async Task EnsureReadyAsync_Should_Throw_When_Consolidation_Receipt_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/harvest-manifest.json",
                FixtureLoader.Load("Harvest/harvest-manifest-no-receipt.json"))
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/runtimes/win-x64/native/SDL2.dll", "<bytes>")
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/licenses/_consolidated/sdl2/LICENSE.txt", "MIT");

        var validator = NewValidator(world);

        var ex = await Assert.ThrowsAsync<CakeException>(async () =>
            await validator.EnsureReadyAsync(TestFamily(), CancellationToken.None));

        await Assert.That(ex!.Message).Contains("lacks a consolidation receipt");
        await Assert.That(ex!.Message).Contains("Re-run ConsolidateHarvest");
    }

    [Test]
    public async Task EnsureReadyAsync_Should_Throw_When_Zero_Successful_Rids()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/harvest-manifest.json",
                FixtureLoader.Load("Harvest/harvest-manifest-zero-rids.json"));

        var validator = NewValidator(world);

        var ex = await Assert.ThrowsAsync<CakeException>(async () =>
            await validator.EnsureReadyAsync(TestFamily(), CancellationToken.None));

        await Assert.That(ex!.Message).Contains("reports zero successful RIDs");
    }

    [Test]
    public async Task EnsureReadyAsync_Should_Throw_When_Zero_License_Entries()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/harvest-manifest.json",
                FixtureLoader.Load("Harvest/harvest-manifest-zero-licenses.json"))
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/runtimes/win-x64/native/SDL2.dll", "<bytes>")
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/licenses/_consolidated/sdl2/LICENSE.txt", "MIT");

        var validator = NewValidator(world);

        var ex = await Assert.ThrowsAsync<CakeException>(async () =>
            await validator.EnsureReadyAsync(TestFamily(), CancellationToken.None));

        await Assert.That(ex!.Message).Contains("zero license entries");
        await Assert.That(ex!.Message).Contains("breaks the compliance surface");
    }

    [Test]
    public async Task EnsureReadyAsync_Should_Throw_When_Payload_Runtime_Subtree_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/harvest-manifest.json",
                FixtureLoader.Load("Harvest/harvest-manifest-ready.json"))
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/licenses/_consolidated/sdl2/LICENSE.txt", "MIT");
        // No runtimes/ directory or contents.

        var validator = NewValidator(world);

        var ex = await Assert.ThrowsAsync<CakeException>(async () =>
            await validator.EnsureReadyAsync(TestFamily(), CancellationToken.None));

        await Assert.That(ex!.Message).Contains("harvest payload directory");
        await Assert.That(ex!.Message).Contains("is missing");
    }

    [Test]
    public async Task EnsureReadyAsync_Should_Throw_When_Payload_Consolidated_Licenses_Subtree_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/harvest-manifest.json",
                FixtureLoader.Load("Harvest/harvest-manifest-ready.json"))
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/runtimes/win-x64/native/SDL2.dll", "<bytes>");
        // No licenses/_consolidated/ directory or contents.

        var validator = NewValidator(world);

        var ex = await Assert.ThrowsAsync<CakeException>(async () =>
            await validator.EnsureReadyAsync(TestFamily(), CancellationToken.None));

        await Assert.That(ex!.Message).Contains("harvest payload directory");
        await Assert.That(ex!.Message).Contains("is missing");
    }

    private static FakeCakeWorldV2 ReadyWorld()
    {
        return FakeCakeWorldV2.CreateWindows()
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/harvest-manifest.json",
                FixtureLoader.Load("Harvest/harvest-manifest-ready.json"))
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/runtimes/win-x64/native/SDL2.dll", "<bytes>")
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/runtimes/linux-x64/native/libSDL2.so", "<bytes>")
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/runtimes/osx-x64/native/libSDL2.dylib", "<bytes>")
            .WithTextFile($"artifacts/harvest_output/{LibraryRef}/licenses/_consolidated/sdl2/LICENSE.txt", "MIT");
    }

    private static HarvestReadinessValidator NewValidator(FakeCakeWorldV2 world)
    {
        var context = world.CreateBuildContext();
        return new HarvestReadinessValidator(world.CakeContext, context.Paths, world.Log);
    }

    private static PackageFamilyConfig TestFamily() => new()
    {
        Name = FamilyName,
        TagPrefix = "sdl2-core/",
        LibraryRef = LibraryRef,
        ManagedProject = "src/SDL2.Core/SDL2.Core.csproj",
        NativeProject = "src/native/SDL2.Core.Native/SDL2.Core.Native.csproj",
        DependsOn = ImmutableList<string>.Empty,
        ChangePaths = ImmutableList<string>.Empty,
    };
}
