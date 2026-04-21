using System.Collections.Immutable;
using Build.Context.Models;
using Build.Domain.Packaging;

namespace Build.Tests.Unit.Domain.Packaging;

/// <summary>
/// Post-B2 extraction of the topological sort previously inlined in
/// <c>PackageTaskRunner</c>. Covers the ADR-003 §2.5 invariant ("depends_on is not
/// automatic scope expansion") plus cycle detection + deterministic tie-breaking.
/// </summary>
public sealed class FamilyTopologyHelpersTests
{
    [Test]
    public async Task TryOrderByDependencies_Should_Sort_Independent_Families_Alphabetically()
    {
        var selected = new[]
        {
            CreateFamily("sdl2-mixer"),
            CreateFamily("sdl2-core"),
            CreateFamily("sdl2-image"),
        };

        var ordered = FamilyTopologyHelpers.TryOrderByDependencies(selected, out var result, out var error);

        await Assert.That(ordered).IsTrue();
        await Assert.That(error).IsEmpty();
        await Assert.That(result.Select(f => f.Name)).IsEquivalentTo(["sdl2-core", "sdl2-image", "sdl2-mixer"]);
    }

    [Test]
    public async Task TryOrderByDependencies_Should_Place_Satellite_After_Its_InScope_Core()
    {
        var selected = new[]
        {
            CreateFamily("sdl2-image", dependsOn: ["sdl2-core"]),
            CreateFamily("sdl2-core"),
        };

        var ordered = FamilyTopologyHelpers.TryOrderByDependencies(selected, out var result, out var error);

        await Assert.That(ordered).IsTrue();
        await Assert.That(error).IsEmpty();
        await Assert.That(result[0].Name).IsEqualTo("sdl2-core");
        await Assert.That(result[1].Name).IsEqualTo("sdl2-image");
    }

    [Test]
    public async Task TryOrderByDependencies_Should_Ignore_Dependencies_Outside_Selected_Scope()
    {
        // ADR-003 §2.5: depends_on does NOT auto-expand scope. A satellite selected alone
        // may ship without its core packing in the same invocation.
        var selected = new[]
        {
            CreateFamily("sdl2-image", dependsOn: ["sdl2-core"]),
        };

        var ordered = FamilyTopologyHelpers.TryOrderByDependencies(selected, out var result, out var error);

        await Assert.That(ordered).IsTrue();
        await Assert.That(error).IsEmpty();
        await Assert.That(result.Select(f => f.Name)).IsEquivalentTo(["sdl2-image"]);
    }

    [Test]
    public async Task TryOrderByDependencies_Should_Detect_Cycle_And_Surface_Family_Names()
    {
        var selected = new[]
        {
            CreateFamily("sdl2-a", dependsOn: ["sdl2-b"]),
            CreateFamily("sdl2-b", dependsOn: ["sdl2-a"]),
        };

        var ordered = FamilyTopologyHelpers.TryOrderByDependencies(selected, out var result, out var error);

        await Assert.That(ordered).IsFalse();
        await Assert.That(result).IsEmpty();
        await Assert.That(error).Contains("cycle");
        await Assert.That(error).Contains("sdl2-a");
        await Assert.That(error).Contains("sdl2-b");
    }

    [Test]
    public async Task TryOrderByDependencies_Should_Handle_Empty_Selection()
    {
        var ordered = FamilyTopologyHelpers.TryOrderByDependencies([], out var result, out var error);

        await Assert.That(ordered).IsTrue();
        await Assert.That(result).IsEmpty();
        await Assert.That(error).IsEmpty();
    }

    private static PackageFamilyConfig CreateFamily(string name, string[]? dependsOn = null)
    {
        return new PackageFamilyConfig
        {
            Name = name,
            TagPrefix = name,
            ManagedProject = $"src/managed/{name}.csproj",
            NativeProject = $"src/native/{name}.Native.csproj",
            LibraryRef = name,
            DependsOn = (dependsOn ?? Array.Empty<string>()).ToImmutableList(),
            ChangePaths = ImmutableList<string>.Empty,
        };
    }
}
