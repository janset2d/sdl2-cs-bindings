using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings;

public sealed class RequiredSdlHeaderTests
{
    private static readonly string[] FunctionNames = ["SDL_Init", "SDL_Quit", "SDL_WasInit"];
    private static readonly string[] ConstantNames = ["SDL_INIT_TIMER", "SDL_INIT_VIDEO", "SDL_INIT_EVERYTHING"];
    [Test]
    public async Task Render_Should_Preserve_Allowlist_Order_Types_Expressions_And_Exact_Text()
    {
        var result = RequiredSdlHeader.Render(FixtureLoader.Load("BindingGeneration/Generation/required-header.txt"), Profile());
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(FixtureLoader.Load("BindingGeneration/Generation/required-rendered.txt"));
    }

    [Test]
    [Arguments("extern DECLSPEC int SDLCALL SDL_Unexpected(void);", "SDL.h declares unexpected direct functions: SDL_Unexpected")]
    [Arguments("#define SDL_INIT_UNEXPECTED 2", "SDL.h declares unexpected SDL_INIT macros: SDL_INIT_UNEXPECTED")]
    public async Task Render_Should_Reject_Unexpected_Surface(string extra, string error)
    {
        var result = RequiredSdlHeader.Render(FixtureLoader.Load("BindingGeneration/Generation/required-header.txt") + extra + "\n", Profile());
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(error);
    }

    [Test]
    [Arguments("SDL_Init", "SDL_Missing", "SDL.h declares unexpected direct functions: SDL_Missing")]
    [Arguments("extern DECLSPEC int SDLCALL SDL_Init(Uint32 flags);", "", "SDL.h is missing required functions: SDL_Init")]
    [Arguments("#define SDL_INIT_VIDEO 0x00000020u /**< comment */", "", "SDL.h is missing required SDL_INIT macros: SDL_INIT_VIDEO")]
    [Arguments("Uint32 flags", "const Uint32 flags", "Unsupported SDL.h parameter declaration: const Uint32 flags")]
    public async Task Render_Should_Refuse_Missing_Or_Unsupported_Declarations(string oldText, string newText, string error)
    {
        var source = FixtureLoader.Load("BindingGeneration/Generation/required-header.txt").Replace(oldText, newText, StringComparison.Ordinal);
        var result = RequiredSdlHeader.Render(source, Profile());
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(error);
    }

    private static BindingGenerationConfig Profile()
    {
        var core = OracleFamilyFixture.Profile("sdl2-core");
        return core with
        {
            RequiredFunctions = [.. FunctionNames.Select(name => core.RequiredFunctions[0] with { Name = name })],
            RequiredConstants = [.. ConstantNames.Select(name => core.RequiredConstants[0] with { Name = name })],
        };
    }
}
