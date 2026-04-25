using System.Collections.Immutable;
using Build.Context.Models;
using Build.Domain.Packaging;

namespace Build.Tests.Unit.Domain.Packaging;

/// <summary>
/// Tests for <see cref="FamilyTopologyHelpers"/>.
/// These cases cover dependency ordering, cycle detection, and the rule that
/// <c>depends_on</c> does not automatically expand the selected scope.
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
        // depends_on does not auto-expand scope. A satellite selected alone may ship
        // without packing its core in the same invocation.
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
