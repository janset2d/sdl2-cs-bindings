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

    [Test]
    public async Task Default_Should_Exclude_SDL_main_From_Emit()
    {
        // SDL_main has no SDL2.dll / libSDL2-2.0.so export — application-defined symbol.
        // Emitting a P/Invoke for it would raise EntryPointNotFoundException at runtime.
        var config = Sdl2CoreGenerationConfig.Default;

        await Assert.That(config.ExcludedFunctionNames).Contains("SDL_main");
    }

    [Test]
    public async Task Default_Should_Exclude_SDL_DYNAPI_entry_From_Emit()
    {
        // SDL_DYNAPI_entry is SDL2's dynamic-API bootstrap handler — declared in
        // SDL_dynapi.c (source file, not header) and exported by the runtime SDL2
        // library for its own loader. Consumer bindings should never bind to it.
        var config = Sdl2CoreGenerationConfig.Default;

        await Assert.That(config.ExcludedFunctionNames).Contains("SDL_DYNAPI_entry");
    }

    [Test]
    public async Task Default_RequiredFunctions_Should_Carry_SDL_Init_Family()
    {
        // SDL.h is excluded from header-set parsing because per-header parsing of an
        // umbrella header collapses isolation. The 5 base init/shutdown functions
        // declared only in SDL.h are recovered via the RequiredFunctions fallback
        // list; the translator merges them into the Neutral view.
        var config = Sdl2CoreGenerationConfig.Default;

        var names = config.RequiredFunctions.Select(f => f.Name).ToList();
        await Assert.That(names).Contains("SDL_Init");
        await Assert.That(names).Contains("SDL_InitSubSystem");
        await Assert.That(names).Contains("SDL_QuitSubSystem");
        await Assert.That(names).Contains("SDL_WasInit");
        await Assert.That(names).Contains("SDL_Quit");
    }
}
