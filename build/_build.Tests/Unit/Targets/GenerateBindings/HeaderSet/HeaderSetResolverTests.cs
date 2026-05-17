using Build.Targets.GenerateBindings.HeaderSet;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using static Build.Tests.Fixtures.BindingGenerationFixture;

namespace Build.Tests.Unit.Targets.GenerateBindings.HeaderSet;

public sealed class HeaderSetResolverTests
{
    [Test]
    public async Task Resolve_Should_Return_Discovered_Header_Paths()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_system.h", "/* system */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_video.h", "/* video */");
        var resolver = new HeaderSetResolver(world.CakeContext);

        var result = resolver.Resolve(
            Sdl2CoreConfig(),
            world.RepoRoot.Combine("vcpkg_installed"),
            world.RepoRoot.Combine("synthetic-headers"),
            "x64-linux-hybrid");

        await Assert.That(result.Headers.Select(path => path.GetFilename().FullPath))
            .IsEquivalentTo(["SDL_system.h", "SDL_video.h"]);
    }

    [Test]
    public async Task Resolve_Should_Exclude_Non_Core_Headers_Per_Manifest_Lists()
    {
        // Filters come from BindingGenerationConfig.HeaderSet.{ExcludedHeaders,
        // ExcludedHeaderPrefixes}. The fixture's Sdl2CoreConfig() mirrors the live
        // manifest: umbrella (SDL.h), pragma-pack scaffolding (begin_code/close_code),
        // satellite umbrellas (SDL_image/mixer/net/ttf and SDL2_* gfx prefix),
        // OpenGL/GLES sub-headers (SDL_opengl_glext, SDL_opengles2_*), and test
        // scaffolding (SDL_test* prefix).
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL.h", "/* umbrella */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/begin_code.h", "/* begin */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/close_code.h", "/* close */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_image.h", "/* image satellite */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_mixer.h", "/* mixer satellite */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_ttf.h", "/* ttf satellite */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_net.h", "/* net satellite */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL2_framerate.h", "/* gfx satellite */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL2_gfxPrimitives.h", "/* gfx satellite */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_opengl.h", "/* gl umbrella, 0 SDL fns */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_opengles.h", "/* gles1 wrapper */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_opengles2.h", "/* gles2 wrapper */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_egl.h", "/* egl wrapper */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_opengl_glext.h", "/* gl sub-header */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_opengles2_gl2.h", "/* gles sub-header */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_test.h", "/* test umbrella */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_test_assert.h", "/* test assert */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_video.h", "/* video */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_vulkan.h", "/* vulkan — has SDL fns, kept */");
        var resolver = new HeaderSetResolver(world.CakeContext);

        var result = resolver.Resolve(
            Sdl2CoreConfig(),
            world.RepoRoot.Combine("vcpkg_installed"),
            world.RepoRoot.Combine("synthetic-headers"),
            "x64-linux-hybrid");

        await Assert.That(result.Headers.Select(path => path.GetFilename().FullPath))
            .IsEquivalentTo(["SDL_video.h", "SDL_vulkan.h"]);
    }

    [Test]
    public async Task Resolve_Should_Throw_When_Include_Directory_Missing()
    {
        var world = FakeCakeWorld.CreateLinux();
        var resolver = new HeaderSetResolver(world.CakeContext);

        var exception = Assert.Throws<CakeException>(() =>
            resolver.Resolve(
                Sdl2CoreConfig(),
                world.RepoRoot.Combine("vcpkg_installed"),
                world.RepoRoot.Combine("synthetic-headers"),
                "x64-linux-hybrid"));

        await Assert.That(exception!.Message).Contains("include directory was not found");
    }

    [Test]
    public async Task Resolve_Should_Throw_When_Include_Directory_Has_No_Matching_Headers()
    {
        var world = FakeCakeWorld.CreateLinux();
        world.FileSystem.GetDirectory(
            world.RepoRoot
                .Combine("vcpkg_installed")
                .Combine("x64-linux-hybrid")
                .Combine("include")
                .Combine("SDL2"))
            .Create();
        var resolver = new HeaderSetResolver(world.CakeContext);

        var exception = Assert.Throws<CakeException>(() =>
            resolver.Resolve(
                Sdl2CoreConfig(),
                world.RepoRoot.Combine("vcpkg_installed"),
                world.RepoRoot.Combine("synthetic-headers"),
                "x64-linux-hybrid"));

        await Assert.That(exception!.Message).Contains("No headers matching");
    }
}
