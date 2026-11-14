using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;

namespace Build.Tests.Scenarios.GenerateBindings.PostProcess;

public sealed class PublicTypedMethodProjectionTests
{
    [Test]
    public async Task WriteAsync_Should_Emit_The_Shared_Public_File_With_Active_Platform_Attributes()
    {
        var world = PostProcessFixture.World().WithTextFile("Janset.SDL2.Core/Generated/Modern/SDL.g.cs", PostProcessFixture.Source("publicModern"));
        var config = await PostProcessFixture.ConfigAsync(world);
        await PublicTypedMethodProjection.WriteAsync(new GeneratedSyntaxFiles(world.CakeContext),
            world.RepoRoot.Combine("Janset.SDL2.Core/Generated/Modern"), "core", config);
        var path = world.RepoRoot.CombineWithFilePath("Janset.SDL2.Core/Generated/Public/SDL.g.cs");
        await Assert.That(world.FileSystem.GetFile(path).Exists).IsTrue();
        var output = world.ReadAllText("Janset.SDL2.Core/Generated/Public/SDL.g.cs");
        await Assert.That(output).Contains("public static byte* SDL_GetError() => SDLNative.SDL_GetError();");
        await Assert.That(output).Contains("#if NET5_0_OR_GREATER\n    [SupportedOSPlatform(\"android\")]\n#endif");
        await Assert.That(output).DoesNotContain("\r");
        await Assert.That(output).DoesNotContain("\uFEFF");
    }
}
