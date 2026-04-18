using Build.Modules.Packaging;

namespace Build.Tests.Unit.Modules.Packaging;

public class SmokeScopeComparatorTests
{
    [Test]
    public async Task Compare_Returns_Match_When_Csproj_References_Exactly_Expected_Set()
    {
        var csproj = Csproj(
            "Janset.SDL2.Core",
            "Janset.SDL2.Image",
            "Janset.SDL2.Mixer");

        var result = SmokeScopeComparator.Compare(csproj, ["Janset.SDL2.Core", "Janset.SDL2.Image", "Janset.SDL2.Mixer"]);

        await Assert.That(result.IsMatch).IsTrue();
        await Assert.That(result.Missing.Count).IsEqualTo(0);
        await Assert.That(result.Unexpected.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Compare_Reports_Missing_PackageReferences_When_Csproj_Does_Not_Cover_Expected_Family()
    {
        // Common failure mode: manifest adds sdl2-net (concrete), runner auto-expands,
        // but PackageConsumer.Smoke.csproj still references only the older 5-family set.
        var csproj = Csproj(
            "Janset.SDL2.Core",
            "Janset.SDL2.Image",
            "Janset.SDL2.Mixer",
            "Janset.SDL2.Ttf",
            "Janset.SDL2.Gfx");

        var result = SmokeScopeComparator.Compare(
            csproj,
            ["Janset.SDL2.Core", "Janset.SDL2.Image", "Janset.SDL2.Mixer", "Janset.SDL2.Ttf", "Janset.SDL2.Gfx", "Janset.SDL2.Net"]);

        await Assert.That(result.IsMatch).IsFalse();
        await Assert.That(result.Missing).IsEquivalentTo(["Janset.SDL2.Net"]);
        await Assert.That(result.Unexpected.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Compare_Reports_Unexpected_PackageReferences_When_Csproj_References_A_Family_The_Manifest_Does_Not()
    {
        var csproj = Csproj(
            "Janset.SDL2.Core",
            "Janset.SDL2.Image",
            "Janset.SDL3.Core");

        var result = SmokeScopeComparator.Compare(csproj, ["Janset.SDL2.Core", "Janset.SDL2.Image"]);

        await Assert.That(result.IsMatch).IsFalse();
        await Assert.That(result.Missing.Count).IsEqualTo(0);
        await Assert.That(result.Unexpected).IsEquivalentTo(["Janset.SDL3.Core"]);
    }

    [Test]
    public async Task Compare_Reports_Both_Missing_And_Unexpected_Simultaneously()
    {
        var csproj = Csproj(
            "Janset.SDL2.Core",
            "Janset.SDL2.Image",
            "Janset.SDL3.Gfx");

        var result = SmokeScopeComparator.Compare(
            csproj,
            ["Janset.SDL2.Core", "Janset.SDL2.Mixer"]);

        await Assert.That(result.IsMatch).IsFalse();
        await Assert.That(result.Missing).IsEquivalentTo(["Janset.SDL2.Mixer"]);
        await Assert.That(result.Unexpected).IsEquivalentTo(["Janset.SDL2.Image", "Janset.SDL3.Gfx"]);
    }

    [Test]
    public async Task Compare_Ignores_Non_Janset_PackageReferences()
    {
        // Smoke csprojs also reference TUnit, PolySharp, System.Memory, etc. — those are
        // test-infrastructure deps, not family packages, and must never participate in
        // the scope comparison.
        var csproj = Csproj(
            "Janset.SDL2.Core",
            "TUnit",
            "PolySharp",
            "System.Memory");

        var result = SmokeScopeComparator.Compare(csproj, ["Janset.SDL2.Core"]);

        await Assert.That(result.IsMatch).IsTrue();
    }

    [Test]
    public async Task Compare_Is_Case_Insensitive_On_Both_Sides()
    {
        var csproj = Csproj("janset.sdl2.CORE");

        var result = SmokeScopeComparator.Compare(csproj, ["Janset.SDL2.Core"]);

        await Assert.That(result.IsMatch).IsTrue();
    }

    [Test]
    public async Task Compare_Tolerates_Empty_Csproj_PackageReferences_Section()
    {
        // Degenerate csproj with no PackageReference entries at all — the check should
        // flag every expected family as missing rather than silently pass.
        const string csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;

        var result = SmokeScopeComparator.Compare(csproj, ["Janset.SDL2.Core", "Janset.SDL2.Image"]);

        await Assert.That(result.IsMatch).IsFalse();
        await Assert.That(result.Missing).IsEquivalentTo(["Janset.SDL2.Core", "Janset.SDL2.Image"]);
    }

    private static string Csproj(params string[] packageReferenceIdentities)
    {
        var references = string.Join(
            Environment.NewLine,
            packageReferenceIdentities.Select(identity => $"    <PackageReference Include=\"{identity}\" VersionOverride=\"1.0.0\" />"));

        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
            {references}
              </ItemGroup>
            </Project>
            """;
    }
}
