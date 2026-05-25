using Janset.SDL2.AbiTests.Infrastructure.Classification;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.Pure;

public sealed class TimerAbiTests
{
    [Test]
    [Category(AbiCategories.HeaderCoverage)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlTimer)]
    public async Task SDLGetTicks_Should_Return_Value_Not_Greater_Than_64Bit_Ticks()
    {
        uint ticks = SDL_GetTicks();
        ulong ticks64 = SDL_GetTicks64();

        await Assert.That(ticks64).IsGreaterThanOrEqualTo(ticks);
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlTimer)]
    public async Task SDLGetTicks64_Should_Not_Move_Backward_Between_Immediate_Reads()
    {
        ulong first = SDL_GetTicks64();
        ulong second = SDL_GetTicks64();

        await Assert.That(second).IsGreaterThanOrEqualTo(first);
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlTimer)]
    public async Task SDLGetPerformanceCounter_Should_Not_Move_Backward_Between_Immediate_Reads()
    {
        ulong first = SDL_GetPerformanceCounter();
        ulong second = SDL_GetPerformanceCounter();

        await Assert.That(second).IsGreaterThanOrEqualTo(first);
    }

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
