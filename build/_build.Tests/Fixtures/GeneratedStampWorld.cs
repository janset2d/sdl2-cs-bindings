using Build.Data.BindingGeneration;
using Build.Data.Manifest;
using Build.Host.Cake;

namespace Build.Tests.Fixtures;

internal static class GeneratedStampWorld
{
    public static FakeCakeWorld Create()
    {
        var world = FakeCakeWorld.CreateLinux().WithManifestObject(ManifestFixture.RealManifest).WithScope("sdl2-image");
        SeedInputs(world);
        world
            .WithTextFile("vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_image.h", "image\n")
            .WithTextFile("vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL.h", "umbrella\n")
            .WithTextFile("vcpkg_installed/x64-windows-hybrid/include/SDL2/nested/transitive.h", "transitive\n")
            .WithTextFile("spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern/z.g.cs", "z\n")
            .WithTextFile("spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Compat/a.g.cs", "a\n");
        return world;
    }

    public static async Task SealAsync(FakeCakeWorld world, string family = "sdl2-image")
    {
        var context = world.CreateBuildContext();
        var configurations = new BindingGenerationConfigRepository(new ManifestRepository(context, context.Paths.GetManifestFile()));
        var profile = configurations.Load(family).Value;
        var inputs = new GeneratedStampInputs(context, context.Paths);
        var generated = await inputs.ReadGeneratedAsync(context.Paths.RepoRoot.Combine(profile.GeneratedRoot), CancellationToken.None);
        var stamp = await inputs.CaptureAsync(profile, "x64-windows-hybrid", true, generated, CancellationToken.None);
        stamp = await inputs.CaptureHeadersAsync(stamp, profile, CancellationToken.None) ?? stamp;
        await new GeneratedStampRepository(context).SaveAsync(context.Paths.RepoRoot.Combine(profile.GeneratedRoot), stamp);
    }

    public static void SeedInputs(FakeCakeWorld world)
    {
        var inputs = CakeJsonExtensions.DeserializeJson<Dictionary<string, string>>(FixtureLoader.Load("BindingGeneration/stamp-inputs.json"))!;

        foreach (var input in inputs)
        {
            world.WithTextFile(input.Key, input.Value);
        }
    }
}
