namespace Janset.SDL2.AbiTests.Infrastructure;

internal static class SdlHelpers
{
    private const float _floatTolerance = 0.00001f;

    public static bool ApproximatelyEqual(float actual, float expected)
    {
        return Math.Abs(actual - expected) <= _floatTolerance;
    }
}
