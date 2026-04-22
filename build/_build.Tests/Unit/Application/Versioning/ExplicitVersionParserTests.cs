using Build.Application.Versioning;
using NuGet.Versioning;

namespace Build.Tests.Unit.Application.Versioning;

/// <summary>
/// Tests for <see cref="ExplicitVersionParser.ParseCliEntries"/>: the single pure-function
/// entry point that validates <c>"family=semver"</c> strings into a typed dictionary.
/// The <c>--versions-file</c> path is tested through the DI composition root
/// (ProgramCompositionRootTests) where Cake's <c>IFileSystem</c> is exercised via fakes.
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
        await Assert.That(result.ContainsKey("sdl2-core")).IsTrue();
        await Assert.That(result["sdl2-core"]).IsEqualTo(NuGetVersion.Parse("2.32.0"));
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
        await Assert.That(result["sdl2-core"]).IsEqualTo(NuGetVersion.Parse("2.32.0-ci.123"));
        await Assert.That(result["sdl2-image"]).IsEqualTo(NuGetVersion.Parse("2.8.0-ci.123"));
        await Assert.That(result["sdl2-ttf"]).IsEqualTo(NuGetVersion.Parse("2.24.0-ci.123"));
    }

    [Test]
    public async Task ParseCliEntries_Should_Be_Case_Insensitive_On_Family_Key()
    {
        var result = ExplicitVersionParser.ParseCliEntries(["SDL2-Core=2.32.0"]);

        await Assert.That(result.ContainsKey("sdl2-core")).IsTrue();
        await Assert.That(result.ContainsKey("SDL2-Core")).IsTrue();
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
        await Assert.That(result.ContainsKey("sdl2-core")).IsTrue();
    }

}
