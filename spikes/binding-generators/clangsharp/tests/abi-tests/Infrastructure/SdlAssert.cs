using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using SDL2;
using static Janset.SDL2.AbiTests.Infrastructure.SdlHelpers;

namespace Janset.SDL2.AbiTests.Infrastructure;

internal static class SdlAssert
{
    public static async Task Success(int result, string operation)
    {
        await Assert.That(result).IsEqualTo(0).Because($"{operation} failed: {SdlError.Current}");
    }

    public static async Task True(SDL_bool value, string operation)
    {
        await Assert.That(value).IsEqualTo(SDL_bool.SDL_TRUE).Because(operation);
    }

    public static async Task AssertRect(SDL_Rect actual, SDL_Rect expected)
    {
        await Assert.That(actual.x).IsEqualTo(expected.x);
        await Assert.That(actual.y).IsEqualTo(expected.y);
        await Assert.That(actual.w).IsEqualTo(expected.w);
        await Assert.That(actual.h).IsEqualTo(expected.h);
    }

    public static async Task AssertFRect(SDL_FRect actual, SDL_FRect expected)
    {
        await Assert.That(ApproximatelyEqual(actual.x, expected.x)).IsTrue();
        await Assert.That(ApproximatelyEqual(actual.y, expected.y)).IsTrue();
        await Assert.That(ApproximatelyEqual(actual.w, expected.w)).IsTrue();
        await Assert.That(ApproximatelyEqual(actual.h, expected.h)).IsTrue();
    }

    public static async Task AssertFPoint(SDL_FPoint actual, SDL_FPoint expected)
    {
        await Assert.That(ApproximatelyEqual(actual.x, expected.x)).IsTrue();
        await Assert.That(ApproximatelyEqual(actual.y, expected.y)).IsTrue();
    }
}
