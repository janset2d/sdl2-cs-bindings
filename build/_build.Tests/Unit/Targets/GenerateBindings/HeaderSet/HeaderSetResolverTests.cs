using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Data.Manifest;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Tests.Fixtures;
using Cake.Core;

namespace Build.Tests.Unit.Targets.GenerateBindings.HeaderSet;

public sealed class HeaderSetResolverTests
{
    [Test]
    public async Task Resolve_Should_Return_Headers_In_Declared_Order_Not_Discovery_Or_Name_Order()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_system.h", "/* system */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_video.h", "/* video */");
        var config = BindingGenerationFixture.Sdl2CoreConfig() with
        {
            HeaderSet = new HeaderSetConfig
            {
                IncludeDir = "include/SDL2",
                Headers = [new HeaderEntryConfig { Order = 20, Name = "SDL_system.h" }, new HeaderEntryConfig { Order = 10, Name = "SDL_video.h" }],
            },
        };

        var result = new HeaderSetResolver(world.CakeContext).Resolve(config, world.RepoRoot.Combine("vcpkg_installed"),
            world.RepoRoot.Combine("shims"), "x64-linux-hybrid");

        await Assert.That(string.Join(',', result.Headers.Select(path => path.GetFilename().FullPath))).IsEqualTo("SDL_video.h,SDL_system.h");
        await Assert.That(result.IncludeRoot.FullPath).IsEqualTo("/repo/vcpkg_installed/x64-linux-hybrid/include");
    }

    [Test]
    [Arguments("sdl2-core", "SDL_assert.h,SDL_atomic.h,SDL_audio.h,SDL_bits.h,SDL_blendmode.h,SDL_clipboard.h,SDL_cpuinfo.h,SDL_endian.h,SDL_error.h," +
        "SDL_events.h,SDL_filesystem.h,SDL_gamecontroller.h,SDL_gesture.h,SDL_guid.h,SDL_haptic.h,SDL_hidapi.h,SDL_hints.h,SDL_joystick.h,SDL_keyboard.h," +
        "SDL_keycode.h,SDL_loadso.h,SDL_locale.h,SDL_log.h,SDL_main.h,SDL_messagebox.h,SDL_metal.h,SDL_misc.h,SDL_mouse.h,SDL_mutex.h,SDL_pixels.h," +
        "SDL_platform.h,SDL_power.h,SDL_quit.h,SDL_rect.h,SDL_render.h,SDL_revision.h,SDL_rwops.h,SDL_scancode.h,SDL_sensor.h,SDL_shape.h,SDL_stdinc.h," +
        "SDL_surface.h,SDL_system.h,SDL_syswm.h,SDL_thread.h,SDL_timer.h,SDL_touch.h,SDL_types.h,SDL_version.h,SDL_video.h,SDL_vulkan.h")]
    [Arguments("sdl2-image", "SDL_image.h")]
    [Arguments("sdl2-ttf", "SDL_ttf.h")]
    [Arguments("sdl2-mixer", "SDL_mixer.h")]
    [Arguments("sdl2-gfx", "SDL2_framerate.h,SDL2_gfxPrimitives.h,SDL2_imageFilter.h,SDL2_rotozoom.h")]
    public async Task Resolve_Should_Select_Exactly_The_Family_Inventory(string family, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var world = FakeCakeWorld.CreateLinux().WithManifestFile(FixtureLoader.Load("Manifest/manifest-real.json"));
        var repository = new BindingGenerationConfigRepository(new ManifestRepository(world.CakeContext,
            world.RepoRoot.CombineWithFilePath("build/manifest.json")));
        var config = repository.Load(family).Value;
        config = config with { HeaderSet = config.HeaderSet! with { Headers = config.HeaderSet.Headers.Reverse() } };

        foreach (var header in expected.Split(',').Reverse())
        {
            world.WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/" + header, "/* selected */");
        }

        world.WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_config.h", "/* not selected */");
        var result = new HeaderSetResolver(world.CakeContext).Resolve(config, world.RepoRoot.Combine("vcpkg_installed"),
            world.RepoRoot.Combine("shims"), "x64-linux-hybrid");

        await Assert.That(string.Join(',', result.Headers.Select(path => path.GetFilename().FullPath))).IsEqualTo(expected);
    }

    [Test]
    public async Task Resolve_Should_Throw_When_Include_Directory_Missing()
    {
        var world = FakeCakeWorld.CreateLinux();
        var exception = Assert.Throws<CakeException>(() => new HeaderSetResolver(world.CakeContext).Resolve(BindingGenerationFixture.Sdl2CoreConfig(),
            world.RepoRoot.Combine("vcpkg_installed"), world.RepoRoot.Combine("shims"), "x64-linux-hybrid"));

        await Assert.That(exception.Message).IsEqualTo(
            "Family 'sdl2-core' include directory was not found at '/repo/vcpkg_installed/x64-linux-hybrid/include/SDL2'.");
    }

    [Test]
    public async Task Resolve_Should_Throw_When_Requested_Header_Is_Missing()
    {
        var world = FakeCakeWorld.CreateLinux().WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_config.h", "/* unrelated */");
        var exception = Assert.Throws<CakeException>(() => new HeaderSetResolver(world.CakeContext).Resolve(BindingGenerationFixture.Sdl2CoreConfig(),
            world.RepoRoot.Combine("vcpkg_installed"), world.RepoRoot.Combine("shims"), "x64-linux-hybrid"));

        await Assert.That(exception.Message).IsEqualTo(
            "Family 'sdl2-core' requested header was not found at '/repo/vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_assert.h'.");
    }
}
