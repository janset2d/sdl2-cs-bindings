using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Host.Cake;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Data.BindingGeneration;

public sealed class GeneratedStampRepositoryTests
{
    [Test]
    public async Task SaveAsync_Should_Write_Deterministic_Json_Without_Wall_Clock_Time()
    {
        var world = FakeCakeWorld.CreateLinux();
        var output = world.RepoRoot.Combine("Generated");
        var stampPath = output.CombineWithFilePath(".generated-stamp");
        var stamp = CreateStamp();
        var repository = new GeneratedStampRepository(world.CakeContext);

        await repository.SaveAsync(output, stamp);
        var first = await world.CakeContext.ReadAllTextAsync(stampPath);
        await repository.SaveAsync(output, stamp);
        var second = await world.CakeContext.ReadAllTextAsync(stampPath);
        var loaded = await repository.LoadAsync(output);

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(second).DoesNotContain("generated_at");
        await Assert.That(second).Contains("schema_version");
        await Assert.That(second).Contains("config_inputs");
        await Assert.That(loaded.Family).IsEqualTo(stamp.Family);
        await Assert.That(loaded.HeaderFingerprint).IsEqualTo(stamp.HeaderFingerprint);
        await Assert.That(loaded.ParseViews.GetRawText()).IsEqualTo(stamp.ParseViews.GetRawText());
    }

    [Test]
    public async Task SaveAsync_Should_Use_LineFeed_When_Host_Is_Windows()
    {
        var world = FakeCakeWorld.CreateWindows();
        var output = world.RepoRoot.Combine("Generated");
        var stamp = CreateStamp();
        var repository = new GeneratedStampRepository(world.CakeContext);
        await repository.SaveAsync(output, stamp);
        var text = world.ReadAllText("Generated/.generated-stamp");
        await Assert.That(text).IsEqualTo(BindingGenerationStampFixture.Sdl2Core);
        await Assert.That(text).DoesNotContain("\r");
        var loaded = await repository.LoadAsync(output);
        await Assert.That(loaded.Family).IsEqualTo(stamp.Family);
        await Assert.That(loaded.SchemaVersion).IsEqualTo(2);
        string?[] expectedViews = ["Neutral", "Windows"];
        await Assert.That(loaded.ParseViews.EnumerateArray().Select(view => view.GetString()))
            .IsEquivalentTo(expectedViews, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static GeneratedStamp CreateStamp() => CakeJsonExtensions.DeserializeJson<GeneratedStamp>(BindingGenerationStampFixture.Sdl2Core)!;
}
