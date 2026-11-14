using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;

namespace Build.Tests.Scenarios.GenerateBindings.PostProcess;

public sealed class UniformOpaqueHandlesTests
{
    [Test]
    public async Task ApplyAsync_Should_Emit_Only_Owned_Handles_And_Rewrite_Satellite_Pointers()
    {
        var world = PostProcessFixture.World().WithTextFile("Janset.SDL2.Ttf/Generated/Modern/SDL_ttf.g.cs", PostProcessFixture.Source("opaqueTtf"));
        var config = await PostProcessFixture.ConfigAsync(world);
        var files = new GeneratedSyntaxFiles(world.CakeContext);
        var directory = world.RepoRoot.Combine("Janset.SDL2.Ttf/Generated/Modern");
        var discovered = await OpaqueHandleEmitRewriter.DiscoverAutoDetectedHandlesAsync(files, directory);
        await Assert.That(discovered).Contains("TTF_Font");
        var (ownedAuto, ownedForce) = config.OpaqueHandles("ttf", false);
        var (applied, forced) = config.OpaqueHandles("ttf", true);
        applied.UnionWith(forced);
        OpaqueHandleEmitRewriter.ReportDrift(new HashSet<string>(["TTF_Font", "SDL_RWops"], StringComparer.Ordinal), ownedAuto, applied, "ttf", world.Log);
        await Assert.That(world.Log.Entries).IsEmpty();
        ownedAuto.UnionWith(ownedForce);
        await UniformOpaqueOwnerMode.EmitConsolidatedHandlesFileIfOwnerAsync(files, directory, ownedAuto, true, "SDL2.Ttf");
        var content = world.ReadAllText("Janset.SDL2.Ttf/Generated/Modern/Handles.g.cs");
        await Assert.That(content).Contains("namespace SDL2.Ttf");
        await Assert.That(content).Contains("public readonly partial struct TTF_Font");
        await Assert.That(content).DoesNotContain("SDL_Window");
        await Assert.That(content).DoesNotContain("SDL_RWops");
        var built = OpaqueHandleEmitRewriter.BuildHandlesFileContent(["TTF_Font"], "SDL2.Ttf");
        await Assert.That(built).Contains("namespace SDL2.Ttf");
        await Assert.That(built).Contains("public readonly partial struct TTF_Font");
        await UniformOpaqueHandles.ApplyAsync(world.CakeContext, directory, directory, config, true, "SDL2.Ttf");
        await Assert.That(world.ReadAllText("Janset.SDL2.Ttf/Generated/Modern/SDL_ttf.g.cs"))
            .Contains("public static partial TTF_Font TTF_OpenFont(byte* file, int ptsize);");
        await Assert.That(world.ReadAllText("Janset.SDL2.Ttf/Generated/Modern/SDL_ttf.g.cs")).DoesNotContain("public partial struct TTF_Font");
    }

    [Test]
    public async Task ApplyAsync_Should_Preserve_Empty_Nonmatching_Trivia_And_Repeated_Configured_Output()
    {
        var nonmatching = PostProcessFixture.Source("nonmatching");
        var world = PostProcessFixture.World()
            .WithTextFile("Janset.SDL2.Ttf/Generated/Modern/Empty.g.cs", "")
            .WithTextFile("Janset.SDL2.Ttf/Generated/Modern/Other.g.cs", nonmatching)
            .WithTextFile("Janset.SDL2.Ttf/Generated/Modern/SDL_ttf.g.cs", PostProcessFixture.Source("opaqueTrivia"));
        var config = await PostProcessFixture.ConfigAsync(world);
        var directory = world.RepoRoot.Combine("Janset.SDL2.Ttf/Generated/Modern");
        await UniformOpaqueHandles.ApplyAsync(world.CakeContext, directory, directory, config, null, "SDL2.Ttf");
        await Assert.That(world.ReadAllText("Janset.SDL2.Ttf/Generated/Modern/Empty.g.cs")).IsEqualTo("");
        await Assert.That(world.ReadAllText("Janset.SDL2.Ttf/Generated/Modern/Other.g.cs")).IsEqualTo(nonmatching);
        var output = world.ReadAllText("Janset.SDL2.Ttf/Generated/Modern/SDL_ttf.g.cs");
        await Assert.That(output).IsEqualTo(PostProcessFixture.Source("opaqueTriviaExpected"));
        var handles = world.ReadAllText("Janset.SDL2.Ttf/Generated/Modern/Handles.g.cs");
        await Assert.That(handles).Contains("public readonly partial struct TTF_Font");
        await UniformOpaqueHandles.ApplyAsync(world.CakeContext, directory, directory, config, null, "SDL2.Ttf");
        await Assert.That(world.ReadAllText("Janset.SDL2.Ttf/Generated/Modern/SDL_ttf.g.cs")).IsEqualTo(output);
        await Assert.That(world.ReadAllText("Janset.SDL2.Ttf/Generated/Modern/Handles.g.cs")).IsEqualTo(handles);
    }
}
