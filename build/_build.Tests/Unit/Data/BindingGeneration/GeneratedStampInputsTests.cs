using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Data.Manifest;
using Build.Tests.Fixtures;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Data.BindingGeneration;

public sealed class GeneratedStampInputsTests
{
    [Test]
    public async Task CaptureAsync_Should_Serialize_Identically_For_Reversed_Output_Discovery_And_Hash_Empty_Git_Blobs()
    {
        var world = GeneratedStampWorld.Create();
        var context = world.CreateBuildContext();
        var family = new BindingGenerationConfigRepository(new ManifestRepository(context, context.Paths.GetManifestFile())).Load("sdl2-image").Value;
        var inputs = new GeneratedStampInputs(context, context.Paths);
        GeneratedFile[] generated = [new("Z.g.cs", ""), new("A.g.cs", "a\n")];
        var first = await inputs.CaptureAsync(family, "x64-windows-hybrid", true, generated, CancellationToken.None);
        var second = await inputs.CaptureAsync(family, "x64-windows-hybrid", true, [.. generated.Reverse()], CancellationToken.None);
        var repository = new GeneratedStampRepository(context);
        await repository.SaveAsync(world.RepoRoot.Combine("one"), first);
        await repository.SaveAsync(world.RepoRoot.Combine("two"), second);
        await Assert.That(world.ReadAllText("one/.generated-stamp")).IsEqualTo(world.ReadAllText("two/.generated-stamp"));
        await Assert.That(first.GeneratedFiles.Keys).IsEquivalentTo(["A.g.cs", "Z.g.cs"], CollectionOrdering.Matching);
        await Assert.That(first.GeneratedFiles["A.g.cs"]).IsEqualTo("git-sha1:78981922613b2afb6025042ff6bd878ac1994e85");
        await Assert.That(first.GeneratedFiles["Z.g.cs"]).IsEqualTo("git-sha1:e69de29bb2d1d6434b8b29ae775ad8c2e48c5391");
        var headers = await inputs.CaptureHeadersAsync(first, family, CancellationToken.None);
        await Assert.That(headers!.HeaderCount).IsEqualTo(1);
        await Assert.That(headers.HeaderInputCount).IsEqualTo(5);
        await Assert.That(headers.SelectedHeaders).IsEquivalentTo(["SDL_image.h"], CollectionOrdering.Matching);
        await Assert.That(world.ReadAllText("one/.generated-stamp")).DoesNotContain("\r");
        await Assert.That(world.ReadAllText("one/.generated-stamp")).EndsWith("}\n");
        await Assert.That(world.ReadAllText("one/.generated-stamp")).DoesNotContain(context.Paths.RepoRoot.FullPath);
    }
}
