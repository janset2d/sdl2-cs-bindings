using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using SDL2;

namespace Janset.SDL2.AbiTests;

public sealed class PublicLayerAbiTests
{
    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.SdlThread)]
    public async Task SDLThreadID_Should_Return_NonZero_Through_Public_Layer()
    {
        ulong threadId = SDL.SDL_ThreadID();

        await Assert.That(threadId).IsNotEqualTo(0UL);
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    public async Task SDLClearError_Should_Clear_Current_Error_Through_Public_Layer()
    {
        SDL.SDL_ClearError();

        string error = GetError();

        await Assert.That(error).IsEmpty();
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    public async Task SDLGetDefaultAssertionHandler_Should_Return_Handler_Through_Public_Layer()
    {
        nint handler = SDL.SDL_GetDefaultAssertionHandler();

        await Assert.That(handler).IsNotEqualTo(0);
    }

    private static unsafe string GetError() => SdlUtf8.FromNullTerminated(SDL.SDL_GetError());
}
