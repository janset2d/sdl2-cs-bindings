using System.Text.Json;
using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class RawAbiChecksTests
{
    [Test]
    public async Task Run_Should_Report_Public_Raw_Class_Imports_And_Deferred_RWops()
    {
        await AssertChecksAsync("mixed-generated.cs.txt", "mixed");
    }

    [Test]
    public async Task Run_Should_Not_Leak_Public_Imports_From_Internal_Containers()
    {
        await AssertChecksAsync("internal-raw.cs.txt", "internal");
    }

    [Test]
    public async Task Run_Should_Respect_Internal_Ancestors_Of_Public_Nested_Containers()
    {
        await AssertChecksAsync("nested-internal-raw.cs.txt", "nested");
    }

    [Test]
    public async Task Run_Should_Report_Missing_Required_Functions_And_Constants()
    {
        var required = new RequiredSurface(SourceStatus.Present,
            new HashSet<string>(["SDL_MissingFunction"], StringComparer.Ordinal), new HashSet<string>(["SDL_MISSING_CONSTANT"], StringComparer.Ordinal));

        await AssertChecksAsync("mixed-generated.cs.txt", "required", required: required);
    }

    [Test]
    public async Task Run_Should_Report_Unresolved_Imports_SysWM_Layouts_Long_And_Wchar()
    {
        await AssertChecksAsync("raw-abi-edge.cs.txt", "edge");
    }

    [Test]
    public async Task Run_Should_Report_One_Duplicate_Tag_With_Canonical_Name_And_Compatibility_Classification()
    {
        await AssertChecksAsync("duplicate-tag.cs.txt", "duplicate");
    }

    [Test]
    public async Task Run_Should_Not_Report_Canonical_Tag_Typedef_Pairs()
    {
        await AssertChecksAsync("canonical-tag.cs.txt", "canonical");
    }

    [Test]
    public async Task Run_Should_Report_Image_Namespace_Drift()
    {
        await AssertChecksAsync("image-namespace-drift.cs.txt", "image", OracleFamilyFixture.Sdl2Image);
    }

    private static async Task AssertChecksAsync(string fixture, string expectedCase, FamilyConfig? config = null, RequiredSurface? required = null)
    {
        var evidence = CSharpEvidenceExtractor.Extract("fixture.cs", FixtureLoader.Load("BindingGeneration/Oracle/" + fixture), ["NET5_0_OR_GREATER"]);
        var checks = RawAbiChecks.Run(config ?? OracleFamilyFixture.Sdl2Core, evidence, required ?? RequiredSurface.Empty);
        using var expected = JsonDocument.Parse(FixtureLoader.Load("BindingGeneration/Oracle/raw-checks.json"));
        var expectedChecks = expected.RootElement.GetProperty(expectedCase).Deserialize<RawAbiCheck[]>()!;

        await Assert.That(checks).IsEquivalentTo(expectedChecks, CollectionOrdering.Matching);
    }
}
