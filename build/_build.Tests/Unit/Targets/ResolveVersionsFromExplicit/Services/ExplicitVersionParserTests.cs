using Build.Data.Versions;
using Build.Targets.ResolveVersionsFromExplicit.Services;
using NuGet.Versioning;

namespace Build.Tests.Unit.Targets.ResolveVersionsFromExplicit.Services;

/// <summary>
/// Tests for <see cref="ExplicitVersionParser.ParseCliEntries"/> and
/// <see cref="ExplicitVersionParser.ParseCommaSeparated"/> — the two pure-function
/// entry points that validate <c>"family=semver"</c> strings into a typed version set.
/// </summary>
public sealed class ExplicitVersionParserTests
{
    // ───────────────────────────────────────────────────────────────────────
    //  ParseCliEntries
    // ───────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ParseCliEntries_Should_Return_Empty_When_No_Entries()
    {
        var result = ExplicitVersionParser.ParseCliEntries([]);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ParseCliEntries_Should_Parse_Single_Entry()
    {
        var result = ExplicitVersionParser.ParseCliEntries(["sdl2-core=2.32.0"]);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(new PackageFamilyId("sdl2-core"))).IsTrue();
        await Assert.That(result.RequireVersion(new PackageFamilyId("sdl2-core"))).IsEqualTo(NuGetVersion.Parse("2.32.0"));
    }

    [Test]
    public async Task ParseCliEntries_Should_Parse_Multiple_Entries()
    {
        var result = ExplicitVersionParser.ParseCliEntries(
        [
            "sdl2-core=2.32.0-ci.123",
            "sdl2-image=2.8.0-ci.123",
            "sdl2-ttf=2.24.0-ci.123",
        ]);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result.RequireVersion(new PackageFamilyId("sdl2-core"))).IsEqualTo(NuGetVersion.Parse("2.32.0-ci.123"));
        await Assert.That(result.RequireVersion(new PackageFamilyId("sdl2-image"))).IsEqualTo(NuGetVersion.Parse("2.8.0-ci.123"));
        await Assert.That(result.RequireVersion(new PackageFamilyId("sdl2-ttf"))).IsEqualTo(NuGetVersion.Parse("2.24.0-ci.123"));
    }

    [Test]
    public async Task ParseCliEntries_Should_Preserve_Input_Family_Casing()
    {
        var result = ExplicitVersionParser.ParseCliEntries(["SDL2-Core=2.32.0"]);

        await Assert.That(result.Contains(new PackageFamilyId("SDL2-Core"))).IsTrue();
        await Assert.That(result.Contains(new PackageFamilyId("sdl2-core"))).IsFalse();
    }

    [Test]
    public async Task ParseCliEntries_Should_Throw_When_Missing_Equals_Separator()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Task.FromResult(ExplicitVersionParser.ParseCliEntries(["sdl2-core:2.32.0"])));
    }

    [Test]
    public async Task ParseCliEntries_Should_Throw_When_Invalid_SemVer()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Task.FromResult(ExplicitVersionParser.ParseCliEntries(["sdl2-core=not-a-version"])));
    }

    [Test]
    public async Task ParseCliEntries_Should_Throw_When_Duplicate_Family()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Task.FromResult(ExplicitVersionParser.ParseCliEntries(
            [
                "sdl2-core=2.32.0",
                "sdl2-core=2.32.1",
            ])));
    }

    [Test]
    public async Task ParseCliEntries_Should_Throw_When_Duplicate_Family_Case_Insensitive()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Task.FromResult(ExplicitVersionParser.ParseCliEntries(
            [
                "sdl2-core=2.32.0",
                "SDL2-Core=2.32.1",
            ])));
    }

    [Test]
    public async Task ParseCliEntries_Should_Skip_Empty_Entries()
    {
        var result = ExplicitVersionParser.ParseCliEntries(["", "  ", "sdl2-core=2.32.0", ""]);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(new PackageFamilyId("sdl2-core"))).IsTrue();
    }

    // ───────────────────────────────────────────────────────────────────────
    //  ParseCommaSeparated
    // ───────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ParseCommaSeparated_Should_Return_Empty_When_Null()
    {
        var result = ExplicitVersionParser.ParseCommaSeparated(null);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ParseCommaSeparated_Should_Return_Empty_When_Whitespace()
    {
        var result = ExplicitVersionParser.ParseCommaSeparated("   ");

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ParseCommaSeparated_Should_Parse_Single_Entry()
    {
        var result = ExplicitVersionParser.ParseCommaSeparated("sdl2-core=2.32.0");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(new PackageFamilyId("sdl2-core"))).IsTrue();
        await Assert.That(result.RequireVersion(new PackageFamilyId("sdl2-core"))).IsEqualTo(NuGetVersion.Parse("2.32.0"));
    }

    [Test]
    public async Task ParseCommaSeparated_Should_Parse_Multiple_Entries()
    {
        var result = ExplicitVersionParser.ParseCommaSeparated(
            "sdl2-core=2.32.0-ci.123,sdl2-image=2.8.0-ci.123,sdl2-ttf=2.24.0-ci.123");

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result.RequireVersion(new PackageFamilyId("sdl2-core"))).IsEqualTo(NuGetVersion.Parse("2.32.0-ci.123"));
        await Assert.That(result.RequireVersion(new PackageFamilyId("sdl2-image"))).IsEqualTo(NuGetVersion.Parse("2.8.0-ci.123"));
        await Assert.That(result.RequireVersion(new PackageFamilyId("sdl2-ttf"))).IsEqualTo(NuGetVersion.Parse("2.24.0-ci.123"));
    }

    [Test]
    public async Task ParseCommaSeparated_Should_Trim_Whitespace()
    {
        var result = ExplicitVersionParser.ParseCommaSeparated(
            "  sdl2-core=2.32.0 , sdl2-image=2.8.0  ");

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.RequireVersion(new PackageFamilyId("sdl2-core"))).IsEqualTo(NuGetVersion.Parse("2.32.0"));
        await Assert.That(result.RequireVersion(new PackageFamilyId("sdl2-image"))).IsEqualTo(NuGetVersion.Parse("2.8.0"));
    }

    [Test]
    public async Task ParseCommaSeparated_Should_Skip_Empty_Segments()
    {
        var result = ExplicitVersionParser.ParseCommaSeparated(
            "sdl2-core=2.32.0,,sdl2-image=2.8.0,,");

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.Contains(new PackageFamilyId("sdl2-core"))).IsTrue();
        await Assert.That(result.Contains(new PackageFamilyId("sdl2-image"))).IsTrue();
    }

    [Test]
    public async Task ParseCommaSeparated_Should_Throw_On_Invalid_SemVer()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Task.FromResult(ExplicitVersionParser.ParseCommaSeparated("sdl2-core=not-a-version")));
    }

    [Test]
    public async Task ParseCommaSeparated_Should_Throw_On_Duplicate_Family()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Task.FromResult(ExplicitVersionParser.ParseCommaSeparated(
                "sdl2-core=2.32.0,sdl2-core=2.32.1")));
    }

}
