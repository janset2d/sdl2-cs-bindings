using System.Text.Json;
using Build.Versioning;
using NuGet.Versioning;

namespace Build.Tests.Unit.Versioning;

/// <summary>
/// Tests for <see cref="PackageFamilyVersionSet"/> — the typed family→version
/// mapping that replaces raw <c>IReadOnlyDictionary&lt;string, NuGetVersion&gt;</c>.
/// </summary>
public sealed class PackageFamilyVersionSetTests
{
    private static readonly PackageFamilyId Sdl2Core = new("sdl2-core");
    private static readonly PackageFamilyId Sdl2Image = new("sdl2-image");
    private static readonly NuGetVersion V232 = NuGetVersion.Parse("2.32.0");
    private static readonly NuGetVersion V280 = NuGetVersion.Parse("2.8.0");

    // ───────────────────────────────────────────────────────────────────────
    //  Construction
    // ───────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Empty_Should_Have_Zero_Count()
    {
        await Assert.That(PackageFamilyVersionSet.Empty.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_Should_Build_Set_From_Single_Entry()
    {
        var set = new PackageFamilyVersionSet([new PackageFamilyVersion(Sdl2Core, V232)]);

        await Assert.That(set.Count).IsEqualTo(1);
        await Assert.That(set.Contains(Sdl2Core)).IsTrue();
    }

    [Test]
    public async Task Constructor_Should_Build_Set_From_Multiple_Entries()
    {
        var set = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(Sdl2Core, V232),
            new PackageFamilyVersion(Sdl2Image, V280),
        ]);

        await Assert.That(set.Count).IsEqualTo(2);
        await Assert.That(set.Contains(Sdl2Core)).IsTrue();
        await Assert.That(set.Contains(Sdl2Image)).IsTrue();
    }

    [Test]
    public async Task Constructor_Should_Throw_ArgumentException_When_Duplicate_Family()
    {
        await Assert.That(() => new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(Sdl2Core, V232),
            new PackageFamilyVersion(Sdl2Core, V280),
        ])).Throws<ArgumentException>();
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Lookups
    // ───────────────────────────────────────────────────────────────────────

    [Test]
    public async Task RequireVersion_Should_Return_Version_When_Family_Present()
    {
        var set = new PackageFamilyVersionSet([new PackageFamilyVersion(Sdl2Core, V232)]);

        await Assert.That(set.RequireVersion(Sdl2Core)).IsEqualTo(V232);
    }

    [Test]
    public async Task RequireVersion_Should_Throw_KeyNotFoundException_With_Family_In_Message_When_Missing()
    {
        var set = PackageFamilyVersionSet.Empty;

        var ex = await Assert.That(() => set.RequireVersion(Sdl2Core)).Throws<KeyNotFoundException>();

        await Assert.That(ex!.Message).Contains("sdl2-core");
    }

    [Test]
    public async Task TryGetVersion_Should_Return_True_And_Version_When_Family_Present()
    {
        var set = new PackageFamilyVersionSet([new PackageFamilyVersion(Sdl2Core, V232)]);

        var found = set.TryGetVersion(Sdl2Core, out var version);

        await Assert.That(found).IsTrue();
        await Assert.That(version).IsEqualTo(V232);
    }

    [Test]
    public async Task TryGetVersion_Should_Return_False_When_Family_Missing()
    {
        var set = PackageFamilyVersionSet.Empty;

        var found = set.TryGetVersion(Sdl2Core, out var version);

        await Assert.That(found).IsFalse();
        await Assert.That(version).IsNull();
    }

    [Test]
    public async Task Contains_Should_Return_True_When_Family_Present()
    {
        var set = new PackageFamilyVersionSet([new PackageFamilyVersion(Sdl2Core, V232)]);

        await Assert.That(set.Contains(Sdl2Core)).IsTrue();
    }

    [Test]
    public async Task Contains_Should_Return_False_When_Family_Missing()
    {
        await Assert.That(PackageFamilyVersionSet.Empty.Contains(Sdl2Core)).IsFalse();
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Ordering, equality, enumeration
    // ───────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Families_Should_Be_Sorted_Ordinal_Exact()
    {
        var set = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(Sdl2Image, V280),
            new PackageFamilyVersion(Sdl2Core, V232),
        ]);

        await Assert.That(set.Families).IsEquivalentTo(new[] { Sdl2Core, Sdl2Image });
    }

    [Test]
    public async Task Equals_Should_Be_True_When_Same_Contents_In_Different_Input_Order()
    {
        var setA = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(Sdl2Core, V232),
            new PackageFamilyVersion(Sdl2Image, V280),
        ]);
        var setB = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(Sdl2Image, V280),
            new PackageFamilyVersion(Sdl2Core, V232),
        ]);

        await Assert.That(setA.Equals(setB)).IsTrue();
        await Assert.That(setA == setB).IsTrue();
    }

    [Test]
    public async Task Enumerator_Should_Yield_All_Pairs_In_Sort_Order()
    {
        var set = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(Sdl2Image, V280),
            new PackageFamilyVersion(Sdl2Core, V232),
        ]);

        var pairs = set.ToList();

        await Assert.That(pairs.Count).IsEqualTo(2);
        await Assert.That(pairs[0]).IsEqualTo(new PackageFamilyVersion(Sdl2Core, V232));
        await Assert.That(pairs[1]).IsEqualTo(new PackageFamilyVersion(Sdl2Image, V280));
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Serialization round-trip (System.Text.Json)
    // ───────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Serialize_Should_Produce_Empty_Object_When_Set_Is_Empty()
    {
        var json = JsonSerializer.Serialize(PackageFamilyVersionSet.Empty);

        await Assert.That(json).IsEqualTo("{}");
    }

    [Test]
    public async Task Serialize_Should_Sort_Keys_Ordinal_Exact()
    {
        var set = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(Sdl2Image, V280),
            new PackageFamilyVersion(Sdl2Core, V232),
        ]);

        var json = JsonSerializer.Serialize(set);

        var coreIndex = json.IndexOf("sdl2-core", StringComparison.Ordinal);
        var imageIndex = json.IndexOf("sdl2-image", StringComparison.Ordinal);
        await Assert.That(coreIndex < imageIndex).IsTrue();
    }

    [Test]
    public async Task Serialize_Should_Use_Normalized_Version_String()
    {
        var version = NuGetVersion.Parse("2.32");
        var set = new PackageFamilyVersionSet([new PackageFamilyVersion(Sdl2Core, version)]);

        var json = JsonSerializer.Serialize(set);

        await Assert.That(json).Contains("\"sdl2-core\":\"2.32.0\"");
    }

    [Test]
    public async Task Deserialize_Should_Reconstruct_Set_From_Existing_VersionsJson_Shape()
    {
        const string json = """
            {
              "sdl2-core": "2.32.0",
              "sdl2-image": "2.8.0"
            }
            """;

        var set = JsonSerializer.Deserialize<PackageFamilyVersionSet>(json);

        await Assert.That(set).IsNotNull();
        await Assert.That(set!.Count).IsEqualTo(2);
        await Assert.That(set.RequireVersion(Sdl2Core)).IsEqualTo(V232);
        await Assert.That(set.RequireVersion(Sdl2Image)).IsEqualTo(V280);
    }

    [Test]
    public async Task Deserialize_Should_Throw_JsonException_When_Duplicate_Family()
    {
        const string json = """{"sdl2-core":"2.32.0","sdl2-core":"2.32.1"}""";

        await Assert.That(() => JsonSerializer.Deserialize<PackageFamilyVersionSet>(json))
            .Throws<JsonException>();
    }

    [Test]
    public async Task Deserialize_Should_Throw_JsonException_When_Invalid_NuGetVersion()
    {
        const string json = """{"sdl2-core":"not-a-version"}""";

        await Assert.That(() => JsonSerializer.Deserialize<PackageFamilyVersionSet>(json))
            .Throws<JsonException>();
    }

    [Test]
    public async Task Roundtrip_Should_Preserve_Set_Equality()
    {
        var original = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(Sdl2Core, V232),
            new PackageFamilyVersion(Sdl2Image, V280),
        ]);

        var json = JsonSerializer.Serialize(original);
        var roundtripped = JsonSerializer.Deserialize<PackageFamilyVersionSet>(json);

        await Assert.That(roundtripped).IsEqualTo(original);
    }
}
