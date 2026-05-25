using Janset.SDL2.AbiTests.Infrastructure.Classification;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.Pure;

public sealed class TimerAbiTests
{
    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlTimer)]
    [UpstreamSdlTest("test/testautomation_timer.c", "timer_getPerformanceCounter")]
    public async Task SDLGetPerformanceCounter_Should_Return_Positive_Value()
    {
        ulong counter = SDL_GetPerformanceCounter();

        await Assert.That(counter).IsGreaterThan(0UL);
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlTimer)]
    [UpstreamSdlTest("test/testautomation_timer.c", "timer_getPerformanceFrequency")]
    public async Task SDLGetPerformanceFrequency_Should_Return_Positive_Value()
    {
        ulong frequency = SDL_GetPerformanceFrequency();

        await Assert.That(frequency).IsGreaterThan(0UL);
    }
}
