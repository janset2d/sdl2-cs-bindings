using Build.Targets.GenerateBindings.HeaderSet;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Tests.Unit.Targets.GenerateBindings.HeaderSet;

public sealed class HeaderSetResolverTests
{
    [Test]
    public async Task ResolveSdl2CoreHeaders_Should_Return_Discovered_Header_Paths()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL.h", "/* umbrella */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_system.h", "/* system */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/begin_code.h", "/* begin */");
        var resolver = new HeaderSetResolver(world.CakeContext);

        var result = resolver.ResolveSdl2CoreHeaders(world.RepoRoot.Combine("vcpkg_installed"), "x64-linux-hybrid");

        await Assert.That(result.Headers.Select(path => path.GetFilename().FullPath))
            .IsEquivalentTo(["SDL.h", "SDL_system.h", "begin_code.h"]);
    }

    [Test]
    public async Task ResolveSdl2CoreHeaders_Should_Throw_When_No_Core_Headers_Are_Found()
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
            resolver.ResolveSdl2CoreHeaders(world.RepoRoot.Combine("vcpkg_installed"), "x64-linux-hybrid"));

        await Assert.That(exception.Message).Contains("No SDL2 headers");
    }
}
