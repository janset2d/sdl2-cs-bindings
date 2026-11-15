using Build.Data;
using Build.Targets.GenerateBindings;
using Build.Tests.Fixtures;
using Build.Validation;
using Cake.Core.IO;
using TUnit.Assertions.Enums;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class GenerateBindingsTaskTests
{
    private const string Artifact = "artifacts/binding-generation/oracle-evidence.md";

    [Test]
    public async Task RunAsync_Should_Plan_All_Families_In_Explicit_Generation_Order()
    {
        var world = FakeCakeWorld.CreateLinux().WithManifestObject(ManifestFixture.RealManifest);
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception).IsNull();
        await Assert.That(world.Log.Entries
            .Where(entry => entry.Message.StartsWith("Plan family:", StringComparison.Ordinal))
            .Select(entry => entry.Message.Split(';')[0]))
            .IsEquivalentTo(["Plan family: sdl2-core", "Plan family: sdl2-image", "Plan family: sdl2-gfx", "Plan family: sdl2-ttf", "Plan family: sdl2-mixer"],
                CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("sdl2-mixer", "sdl2-core", "sdl2-image", "sdl2-image")]
    [Arguments("sdl2-image", "sdl2-core", "sdl2-mixer", "sdl2-core")]
    public async Task RunAsync_Should_Deduplicate_Reversed_Scope_In_Generation_Order_And_Render_In_Presentation_Order(
        string first, string second, string third, string fourth)
    {
        var world = FakeCakeWorld.CreateLinux().WithManifestObject(ManifestFixture.RealManifest).WithEvidence().WithScope(first, second, third, fourth);
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.Log.Entries
            .Where(entry => entry.Message.StartsWith("Evidence family: ", StringComparison.Ordinal))
            .Select(entry => entry.Message))
            .IsEquivalentTo(["Evidence family: sdl2-core", "Evidence family: sdl2-image", "Evidence family: sdl2-mixer"], CollectionOrdering.Matching);
        await Assert.That(world.ReadAllText(Artifact).Split('\n').Where(line => line.StartsWith("## Family:", StringComparison.Ordinal)))
            .IsEquivalentTo(["## Family: sdl2-core", "## Family: sdl2-image", "## Family: sdl2-mixer"], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("unknown", "Manifest does not declare a library with binding_generation matching family 'unknown'.")]
    [Arguments("SDL2-CORE", "Manifest does not declare a library with binding_generation matching family 'SDL2-CORE'.")]
    public async Task RunAsync_Should_Refuse_Unknown_Or_Wrong_Case_Scope_Before_Any_Work(string family, string error)
    {
        var world = FakeCakeWorld
            .CreateLinux()
            .WithManifestObject(ManifestFixture.RealManifest)
            .WithEvidence()
            .WithScope("sdl2-core", family)
            .WithTextFile("external/vcpkg/buildtrees/sdl2/src/version/src/dynapi/SDL2.exports", "malformed");
        await AssertRefusalAsync(world, error);
    }

    [Test]
    public async Task RunAsync_Should_Refuse_Disabled_Scope_Before_Any_Work()
    {
        var manifest = ManifestFixture.RealManifest with
        {
            LibraryManifests = [.. ManifestFixture.RealManifest.LibraryManifests.Select(library => library.Name == "SDL2_image"
                ? library with { BindingGeneration = library.BindingGeneration with { Enabled = false } } : library)]
        };
        var world = FakeCakeWorld
            .CreateLinux()
            .WithManifestObject(manifest)
            .WithEvidence()
            .WithScope("sdl2-core", "sdl2-image")
            .WithTextFile("external/vcpkg/buildtrees/sdl2/src/version/src/dynapi/SDL2.exports", "malformed");
        await AssertRefusalAsync(world, "Family 'sdl2-image' has binding_generation.enabled=false in manifest.");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunAsync_Should_Plan_Without_Installed_Headers_Or_Changing_Any_File(bool existingDiagnostics)
    {
        var world = FakeCakeWorld.CreateLinux().WithManifestObject(ManifestFixture.RealManifest).WithScope("sdl2-mixer", "sdl2-image");

        if (existingDiagnostics)
        {
            world.WithTextFile("artifacts/binding-generation/generation-run.md", "sentinel diagnostics");
        }

        var before = Snapshot(world);
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception).IsNull();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(Snapshot(world)).IsEquivalentTo(before, CollectionOrdering.Matching);
        await Assert.That(world.ProcessInvocations).IsEmpty();
        await Assert.That(world.HostProcessInvocations).IsEmpty();
        await Assert.That(world.Log.Entries.Where(entry => entry.Message.StartsWith("Plan family:", StringComparison.Ordinal)).Select(entry => entry.Message))
            .IsEquivalentTo(["Plan family: sdl2-image; headers: SDL_image.h", "Plan family: sdl2-mixer; headers: SDL_mixer.h"], CollectionOrdering.Matching);
        await Assert.That(world.Log.Entries.Count(entry => entry.Message.StartsWith("dotnet ", StringComparison.Ordinal))).IsEqualTo(4);
    }

    [Test]
    public async Task RunAsync_Should_Report_Both_Severity_Profiles_With_Positive_Dynapi_Evidence_Without_Failing_On_Findings()
    {
        var profile = OracleFamilyFixture.Profile("sdl2-core");
        var world = FakeCakeWorld
            .CreateLinux()
            .WithManifestObject(ManifestFixture.RealManifest)
            .WithEvidence()
            .WithScope("sdl2-core")
            .WithTextFile("vcpkg_installed/vcpkg/blds/sdl2/src/version/src/dynapi/SDL2.exports", FixtureLoader.Load("BindingGeneration/Oracle/coherence.exports"))
            .WithTextFile(profile.GeneratedRoot + "/Compat/sample.g.cs", FixtureLoader.Load("BindingGeneration/Oracle/coherence.cs.txt"))
            .WithTextFile(profile.GeneratedRoot + "/Modern/sample.g.cs", FixtureLoader.Load("BindingGeneration/Oracle/coherence.cs.txt")
                .Replace("DllImport", "LibraryImport", StringComparison.Ordinal)
                .Replace("extern", "partial", StringComparison.Ordinal))
            .WithTextFile(profile.GeneratedRoot + "/Handles.g.cs", FixtureLoader.Load("BindingGeneration/Oracle/ownership-compat.cs.txt"));
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception).IsNull();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.FileExists(Artifact)).IsTrue();
        var markdown = world.ReadAllText(Artifact);
        await Assert.That(markdown).Contains("| Stage1Generator | 1 | 1 |");
        await Assert.That(markdown).Contains("| Stage2Strict | 2 | 0 |");
        await Assert.That(markdown).Contains("Public SDL2 export missing from emitted bindings");
        await Assert.That(markdown).Contains("'SDL_Quit'");
        await Assert.That(markdown).Contains("'SDL_Extra'");
        await Assert.That(markdown).Contains("| SDL_Init | Compat | present and equal |");
        await Assert.That(markdown).Contains("| SDL_Init | Modern | present and equal |");
        await Assert.That(markdown).Contains("Warning: `AlienStruct`: not covered by owned_prefixes.");
        await Assert.That(world.ProcessInvocations).IsEmpty();
        await Assert.That(world.HostProcessInvocations).IsEmpty();
    }

    private static async Task AssertRefusalAsync(FakeCakeWorld world, string error)
    {
        var before = Snapshot(world);
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception).IsTypeOf<Cake.Core.CakeException>();
        await Assert.That(result.Exception!.Message).IsEqualTo(error);
        await Assert.That(world.FileExists(Artifact)).IsFalse();
        await Assert.That(Snapshot(world)).IsEquivalentTo(before, CollectionOrdering.Matching);
        await Assert.That(world.ProcessInvocations).IsEmpty();
        await Assert.That(world.HostProcessInvocations).IsEmpty();
        await Assert.That(world.Log.Entries.Select(entry => entry.Message)).IsEquivalentTo([
            "Warning: Vcpkg directory not specified via --vcpkg-dir. Assuming relative path: /repo/external/vcpkg",
            "Warning: Vcpkg installed directory not specified via --vcpkg-installed-dir. Assuming relative path: /repo/vcpkg_installed"
        ], CollectionOrdering.Matching);
    }

    [Test]
    public async Task RunAsync_Should_Refuse_Ambiguous_Dynapi_Without_Writing_An_Artifact()
    {
        var exports = FixtureLoader.Load("BindingGeneration/Oracle/SDL2.exports");
        var world = FakeCakeWorld
            .CreateLinux()
            .WithManifestObject(ManifestFixture.RealManifest)
            .WithEvidence()
            .WithScope("sdl2-core")
            .WithTextFile("vcpkg_installed/vcpkg/blds/sdl2/src/stale/src/dynapi/SDL2.exports", exports)
            .WithTextFile("external/vcpkg/buildtrees/sdl2/src/current/src/dynapi/SDL2.exports", exports.Replace("SDL_Init", "SDL_Quit", StringComparison.Ordinal));

        await AssertRefusalAsync(world,
            "SDL2 dynapi manifest globs '/repo/external/vcpkg/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports' and " +
            "'/repo/vcpkg_installed/vcpkg/blds/sdl2/src/*/src/dynapi/SDL2.exports' matched multiple candidates: " +
            "'/repo/external/vcpkg/buildtrees/sdl2/src/current/src/dynapi/SDL2.exports', " +
            "'/repo/vcpkg_installed/vcpkg/blds/sdl2/src/stale/src/dynapi/SDL2.exports'. " +
            "This usually means a stale buildtree from a previous vcpkg pin was left on disk after a version downgrade. " +
            "Delete the stale tree under whichever search root holds it before re-running.");
    }

    [Test]
    public async Task RunAsync_Should_Refuse_Dynapi_Parse_Failure_Without_Writing_An_Artifact()
    {
        var world = FakeCakeWorld
            .CreateLinux()
            .WithManifestObject(ManifestFixture.RealManifest)
            .WithEvidence()
            .WithScope("sdl2-core")
            .WithTextFile("vcpkg_installed/vcpkg/blds/sdl2/src/broken/src/dynapi/SDL2.exports", "++ malformed line");

        await AssertRefusalAsync(world,
            "SDL2 dynapi manifest at '/repo/vcpkg_installed/vcpkg/blds/sdl2/src/broken/src/dynapi/SDL2.exports' failed to parse: " +
            "Dynapi manifest '/repo/vcpkg_installed/vcpkg/blds/sdl2/src/broken/src/dynapi/SDL2.exports' line 1: " +
            "export line did not match the canonical Watcom format: '++ malformed line'.");
    }

    [Test]
    public async Task RunAsync_Should_Write_Evidence_Without_Changing_Inputs_Or_Starting_Processes()
    {
        var world = FakeCakeWorld
            .CreateLinux()
            .WithManifestObject(ManifestFixture.RealManifest)
            .WithEvidence()
            .WithTextFile("README.md", "tracked sentinel");
        var before = Snapshot(world);
        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.FileExists(Artifact)).IsTrue();
        var markdown = world.ReadAllText(Artifact);
        await Assert.That(markdown).Contains("### Dynapi coherence\n");
        await Assert.That(markdown).Contains("| Stage1Generator | Evidence Missing | Evidence Missing |");
        await Assert.That(markdown).Contains("| Stage2Strict | Evidence Missing | Evidence Missing |");
        await Assert.That(markdown).Contains("### Required-surface audit\n");
        await Assert.That(markdown).Contains("| SDL_Init | Compat | Evidence Missing |");
        await Assert.That(markdown).Contains("### Ownership audit\n");
        await Assert.That(markdown).Contains("prefix-roster gap, not proof of foreign ownership");
        await Assert.That(markdown.Split('\n').Where(line => line.StartsWith("## Family:", StringComparison.Ordinal)))
            .IsEquivalentTo(["## Family: sdl2-core", "## Family: sdl2-gfx", "## Family: sdl2-image", "## Family: sdl2-mixer", "## Family: sdl2-ttf"],
                CollectionOrdering.Matching);
        await Assert.That(Snapshot(world).Where(entry => !entry.StartsWith(Artifact + "|", StringComparison.Ordinal)))
            .IsEquivalentTo(before, CollectionOrdering.Matching);
        await Assert.That(world.ProcessInvocations).IsEmpty();
        await Assert.That(world.HostProcessInvocations).IsEmpty();
        await Assert.That(world.Log.Entries
            .Where(entry => entry.Message.StartsWith("Evidence family: ", StringComparison.Ordinal))
            .Select(entry => entry.Message))
            .IsEquivalentTo(["Evidence family: sdl2-core", "Evidence family: sdl2-image", "Evidence family: sdl2-gfx",
                "Evidence family: sdl2-ttf", "Evidence family: sdl2-mixer"], CollectionOrdering.Matching);
    }

    private static TargetTestHost<GenerateBindingsTask> CreateHost(FakeCakeWorld world)
        => new TargetTestHost<GenerateBindingsTask>(world).WithServices(services => services.AddData().AddValidators());

    private static string[] Snapshot(FakeCakeWorld world) => [.. world.FileSystem
        .GetDirectory(world.RepoRoot)
        .GetFiles("*", SearchScope.Recursive)
        .Select(file => file.Path.FullPath[(world.RepoRoot.FullPath.Length + 1)..])
        .Order(StringComparer.Ordinal)
        .Select(path => path + "|" + world.ReadAllText(path))];
}
