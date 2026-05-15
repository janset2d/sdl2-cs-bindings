using Build.Targets.GenerateBindings;

namespace Build.Tests.Unit.Targets.GenerateBindings;

public sealed class Sdl2CoreGenerationConfigTests
{
    [Test]
    public async Task Default_Should_Expose_Sdl2_Core_Identity()
    {
        var config = Sdl2CoreGenerationConfig.Default;

        await Assert.That(config.Family).IsEqualTo("sdl2-core");
        await Assert.That(config.LibraryName).IsEqualTo("SDL2");
        await Assert.That(config.Namespace).IsEqualTo("SDL2");
        await Assert.That(config.PrimaryClassName).IsEqualTo("SDL");
        await Assert.That(config.OwnedPrefixes).Contains("SDL_");
    }

    [Test]
    public async Task Default_Should_Mark_SysWMinfo_Typed_Union_As_Deferred()
    {
        var config = Sdl2CoreGenerationConfig.Default;

        await Assert.That(config.DeferredDeclarations).Contains("SDL_SysWMinfo");
        await Assert.That(config.DeferredDeclarations).Contains("SDL_SysWMmsg");
    }
}
