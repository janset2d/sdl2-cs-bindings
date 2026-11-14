using System.Text.Json;
using System.Text.Json.Nodes;
using Build.Data.BindingGeneration.Models;
using Build.Data.Manifest;
using Build.Tests.Fixtures;
using Cake.Core;

namespace Build.Tests.Unit.Data.Manifest;

/// <summary>
/// Sociable round-trip coverage for <see cref="ManifestRepository"/>. Pairs with
/// <see cref="ManifestRepositoryUnitTests"/> per the repository-cohort rule.
/// </summary>
public sealed class ManifestRepositoryRoundTripTests
{
    [Test]
    [Arguments("required_functions/0")]
    [Arguments("required_functions/0/parameters/0")]
    [Arguments("required_constants/0")]
    [Arguments("header_set")]
    [Arguments("header_set/headers/0")]
    [Arguments("opaque_handles")]
    [Arguments("opaque_handles/auto_detect_well_known/0")]
    [Arguments("opaque_handles/force_opaque_exceptions/0")]
    [Arguments("opaque_handles/excluded_candidates/0")]
    [Arguments("flags_enums")]
    [Arguments("flags_enums/allow_list/0")]
    [Arguments("dynapi")]
    public async Task Load_Should_Refuse_Stale_Keys_In_Nested_Generation_Objects(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var json = JsonNode.Parse(FixtureLoader.Load("Manifest/manifest-real.json"))!;
        var node = json["library_manifests"]![0]!["binding_generation"]!;

        foreach (var segment in path.Split('/'))
        {
            node = node is JsonArray ? node[int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture)]! : node[segment]!;
        }

        node["stale_key"] = true;
        var world = FakeCakeWorld.CreateWindows().WithManifestFile(json.ToJsonString());
        var repo = new ManifestRepository(world.CakeContext, world.RepoRoot.CombineWithFilePath("build/manifest.json"));
        var error = Assert.Throws<CakeException>(() => repo.Load());
        await Assert.That(error.Message).Contains("stale_key");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Load_Should_Refuse_Stale_Keys_In_Global_Generation_Objects(bool view)
    {
        var json = JsonNode.Parse(FixtureLoader.Load("Manifest/manifest-real.json"))!;
        var node = view ? json["binding_generation"]!["platform_views"]![0]! : json["binding_generation"]!;
        node["stale_key"] = true;
        var world = FakeCakeWorld.CreateWindows().WithManifestFile(json.ToJsonString());
        var repo = new ManifestRepository(world.CakeContext, world.RepoRoot.CombineWithFilePath("build/manifest.json"));
        var error = Assert.Throws<CakeException>(() => repo.Load());
        await Assert.That(error.Message).Contains("stale_key");
    }

    [Test]
    public async Task Load_Should_Refuse_A_Stale_Generation_Key()
    {
        var json = JsonNode.Parse(FixtureLoader.Load("Manifest/manifest-real.json"))!;
        var block = json["library_manifests"]![0]!["binding_generation"]!;
        block["platform_catalog"] = "sdl2-core";
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BindingGenerationConfig>(block));
        await Assert.That(error.Message).Contains("platform_catalog");
    }

    [Test]
    public async Task Load_Should_Refuse_An_Unknown_Schema()
    {
        var world = FakeCakeWorld.CreateWindows();
        world.WithManifestFile(FixtureLoader.Load("Manifest/manifest-win-x64.json")
            .Replace("\"schema_version\": \"3.0\"", "\"schema_version\": \"99.0\"", StringComparison.Ordinal));
        var repo = new ManifestRepository(world.CakeContext, world.RepoRoot.CombineWithFilePath("build/manifest.json"));

        var error = Assert.Throws<CakeException>(() => repo.Load());
        await Assert.That(error.Message).IsEqualTo("Manifest schema_version '99.0' is unsupported; expected '3.0'.");
    }

    [Test]
    public async Task Load_Should_Deserialize_Valid_Manifest()
    {
        var world = FakeCakeWorld.CreateWindows();
        var manifestPath = world.RepoRoot.CombineWithFilePath("build/manifest.json");
        var repo = new ManifestRepository(world.CakeContext, manifestPath);

        var manifest = repo.Load();

        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest.SchemaVersion).IsEqualTo("3.0");
        await Assert.That(manifest.Runtimes.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(manifest.Runtimes[0].Rid).IsEqualTo("win-x64");
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_File_Missing()
    {
        var world = FakeCakeWorld.CreateWindows();
        var manifestPath = world.RepoRoot.CombineWithFilePath("build/nonexistent.json");
        var repo = new ManifestRepository(world.CakeContext, manifestPath);

        await Assert.That(() => repo.Load()).Throws<CakeException>();
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_File_Is_Invalid_Json()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithTextFile("build/bad-manifest.json", "not json {{{");
        var manifestPath = world.RepoRoot.CombineWithFilePath("build/bad-manifest.json");
        var repo = new ManifestRepository(world.CakeContext, manifestPath);

        await Assert.That(() => repo.Load()).Throws<CakeException>();
    }
}
