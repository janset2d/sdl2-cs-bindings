using System.Collections.Immutable;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using TUnit.Assertions.Enums;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class GenerationInputTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunAsync_Should_Reject_Unknown_Validator_Ids_Before_Any_Process_Or_File_Change(bool execute)
    {
        var manifest = ManifestFixture.RealManifest;
        manifest = manifest with
        {
            LibraryManifests = [.. manifest.LibraryManifests.Select(library => library.Name == "SDL2_image" ? library with
            {
                BindingGeneration = library.BindingGeneration with { Validators = ImmutableDictionary<string, bool>.Empty.Add("unknown-validator", true) },
            } : library)],
        };
        var world = FakeCakeWorld.CreateLinux().WithManifestObject(manifest).WithScope("sdl2-image").WithGeneration(execute);
        await RefusalAsync(world,
            "Binding generation family 'sdl2-image' enables unknown validator id(s): unknown-validator. " +
            "Register the validator or fix build/manifest.json library_manifests[].binding_generation.validators.");
    }

    [Test]
    [Arguments("unknown", "Manifest does not declare a library with binding_generation matching family 'unknown'.")]
    [Arguments("sdl2-image", "Family 'sdl2-image' has binding_generation.enabled=false in manifest.")]
    public async Task RunAsync_Should_Reject_Invalid_Or_Disabled_Generation_Selection_Without_Side_Effects(string family, string error)
    {
        var manifest = ManifestFixture.RealManifest;
        manifest = manifest with
        {
            LibraryManifests = [.. manifest.LibraryManifests.Select(library => library.Name == "SDL2_image" ? library with
            {
                BindingGeneration = library.BindingGeneration with { Enabled = false },
            } : library)],
        };
        var world = FakeCakeWorld.CreateLinux().WithManifestObject(manifest).WithScope(family).WithGeneration();
        await RefusalAsync(world, error);
    }

    [Test]
    [Arguments(false, "Family 'sdl2-image' include directory was not found at '/repo/vcpkg_installed/x64-linux-hybrid/include/SDL2'.")]
    [Arguments(true, "Family 'sdl2-image' requested header was not found at '/repo/vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_image.h'.")]
    public async Task RunAsync_Should_Refuse_Missing_Installed_Inputs_Before_Processes(bool directoryExists, string error)
    {
        var world = FakeCakeWorld.CreateLinux().WithManifestObject(ManifestFixture.RealManifest).WithScope("sdl2-image").WithGeneration();

        if (directoryExists)
        {
            world.WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/another.h", "not the selected header");
        }

        await RefusalAsync(world, error);
    }

    private static async Task RefusalAsync(FakeCakeWorld world, string error)
    {
        var before = Snapshot(world);
        var result = await GenerationExecutionTests.CreateHost(world).RunAsync();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).IsEqualTo(error);
        await Assert.That(world.ProcessInvocations).IsEmpty();
        await Assert.That(world.HostProcessInvocations).IsEmpty();
        await Assert.That(Snapshot(world)).IsEquivalentTo(before, CollectionOrdering.Matching);
    }

    private static string[] Snapshot(FakeCakeWorld world) => [.. world.FileSystem
        .GetDirectory(world.RepoRoot)
        .GetFiles("*", SearchScope.Recursive)
        .OrderBy(file => file.Path.FullPath, StringComparer.Ordinal)
        .Select(file => file.Path.FullPath + "|" + world.ReadAllText(world.RepoRoot.GetRelativePath(file.Path).FullPath))];
}
