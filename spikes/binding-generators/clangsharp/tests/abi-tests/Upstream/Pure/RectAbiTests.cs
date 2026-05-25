using Janset.SDL2.AbiTests.Infrastructure;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using SDL2;
using static Janset.SDL2.AbiTests.Infrastructure.SdlHelpers;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.Pure;

public sealed class RectAbiTests
{
    [Test]
    [Arguments(-1, -1)]
    [Arguments(-1, 0)]
    [Arguments(-1, 1)]
    [Arguments(0, -1)]
    [Arguments(0, 1)]
    [Arguments(1, -1)]
    [Arguments(1, 0)]
    [Arguments(1, 1)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlRect)]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testHasIntersectionPoint")]
    public async Task SDLHasIntersection_Should_Return_False_For_Adjacent_One_Pixel_Rects(int offsetX, int offsetY)
    {
        SDL_Rect rectA = Rect(10, 20, 1, 1);
        SDL_Rect rectB = Rect(10 + offsetX, 20 + offsetY, 1, 1);

        SDL_bool result = HasIntersection(rectA, rectB);

        await Assert.That(result).IsEqualTo(SDL_bool.SDL_FALSE);
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlRect)]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testHasIntersectionInside")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testHasIntersectionOutside")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testHasIntersectionPartial")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testHasIntersectionPoint")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testHasIntersectionEmpty")]
    public async Task SDLHasIntersection_Should_Return_Expected_Result_For_Deterministic_Rect_Cases()
    {
        RectBoolCase[] cases =
        [
            new(Rect(0, 0, 32, 32), Rect(0, 0, 16, 16), SDL_bool.SDL_TRUE),
            new(Rect(0, 0, 32, 32), Rect(33, 33, 32, 32), SDL_bool.SDL_FALSE),
            new(Rect(0, 0, 32, 32), Rect(16, 16, 32, 32), SDL_bool.SDL_TRUE),
            new(Rect(0, 0, 32, 32), Rect(31, 0, 10, 10), SDL_bool.SDL_TRUE),
            new(Rect(0, 0, 32, 32), Rect(-31, 0, 32, 10), SDL_bool.SDL_TRUE),
            new(Rect(0, 0, 32, 32), Rect(0, 31, 10, 10), SDL_bool.SDL_TRUE),
            new(Rect(0, 0, 32, 32), Rect(0, -31, 10, 32), SDL_bool.SDL_TRUE),
            new(Rect(10, 20, 1, 1), Rect(10, 20, 1, 1), SDL_bool.SDL_TRUE),
            new(Rect(10, 20, 0, 0), Rect(10, 20, 10, 10), SDL_bool.SDL_FALSE),
            new(Rect(10, 20, 10, 10), Rect(10, 20, 0, 0), SDL_bool.SDL_FALSE),
            new(Rect(10, 20, 0, 0), Rect(10, 20, 0, 0), SDL_bool.SDL_FALSE),
        ];

        foreach (RectBoolCase testCase in cases)
        {
            SDL_bool result = HasIntersection(testCase.A, testCase.B);

            await Assert.That(result).IsEqualTo(testCase.Expected);
        }
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlRect)]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testIntersectRectInside")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testIntersectRectOutside")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testIntersectRectPartial")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testIntersectRectPoint")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testIntersectRectEmpty")]
    public async Task SDLIntersectRect_Should_Return_Expected_Intersection_For_Deterministic_Rect_Cases()
    {
        RectIntersectionCase[] cases =
        [
            new(Rect(0, 0, 32, 32), Rect(0, 0, 16, 16), SDL_bool.SDL_TRUE, Rect(0, 0, 16, 16)),
            new(Rect(0, 0, 32, 32), Rect(33, 33, 32, 32), SDL_bool.SDL_FALSE, null),
            new(Rect(0, 0, 32, 32), Rect(16, 16, 32, 32), SDL_bool.SDL_TRUE, Rect(16, 16, 16, 16)),
            new(Rect(0, 0, 32, 32), Rect(31, 0, 10, 10), SDL_bool.SDL_TRUE, Rect(31, 0, 1, 10)),
            new(Rect(0, 0, 32, 32), Rect(-31, 0, 32, 10), SDL_bool.SDL_TRUE, Rect(0, 0, 1, 10)),
            new(Rect(0, 0, 32, 32), Rect(0, 31, 10, 10), SDL_bool.SDL_TRUE, Rect(0, 31, 10, 1)),
            new(Rect(0, 0, 32, 32), Rect(0, -31, 10, 32), SDL_bool.SDL_TRUE, Rect(0, 0, 10, 1)),
            new(Rect(10, 20, 1, 1), Rect(10, 20, 1, 1), SDL_bool.SDL_TRUE, Rect(10, 20, 1, 1)),
            new(Rect(10, 20, 0, 0), Rect(10, 20, 10, 10), SDL_bool.SDL_FALSE, null),
            new(Rect(10, 20, 10, 10), Rect(10, 20, 0, 0), SDL_bool.SDL_FALSE, null),
            new(Rect(10, 20, 0, 0), Rect(10, 20, 0, 0), SDL_bool.SDL_FALSE, null),
        ];

        foreach (RectIntersectionCase testCase in cases)
        {
            (SDL_bool result, SDL_Rect intersection) = IntersectRect(testCase.A, testCase.B);

            await Assert.That(result).IsEqualTo(testCase.ExpectedResult);
            if (testCase.ExpectedIntersection is { } expectedIntersection)
            {
                await SdlAssert.AssertRect(intersection, expectedIntersection);
            }
            else if (result == SDL_bool.SDL_FALSE)
            {
                await Assert.That(RectIsEmpty(intersection)).IsTrue();
            }
        }
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlRect)]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testUnionRectInside")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testUnionRectOutside")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testUnionRectEmpty")]
    public async Task SDLUnionRect_Should_Write_Expected_Union_For_Deterministic_Rect_Cases()
    {
        RectResultCase[] cases =
        [
            new(Rect(4, 5, 1, 1), Rect(4, 5, 1, 1), Rect(4, 5, 1, 1)),
            new(Rect(0, 0, 32, 32), Rect(10, 10, 1, 1), Rect(0, 0, 32, 32)),
            new(Rect(0, 0, 32, 32), Rect(1, 1, 30, 30), Rect(0, 0, 32, 32)),
            new(Rect(0, 0, 1, 1), Rect(10, 20, 1, 1), Rect(0, 0, 11, 21)),
            new(Rect(0, 0, 32, 32), Rect(-1, -1, 30, 30), Rect(-1, -1, 33, 33)),
            new(Rect(10, 20, 0, 0), Rect(3, 4, 5, 6), Rect(3, 4, 5, 6)),
            new(Rect(3, 4, 5, 6), Rect(10, 20, 0, 0), Rect(3, 4, 5, 6)),
            new(Rect(10, 20, 0, 0), Rect(30, 40, 0, 0), Rect(0, 0, 0, 0)),
        ];

        foreach (RectResultCase testCase in cases)
        {
            SDL_Rect result = UnionRect(testCase.A, testCase.B);

            await SdlAssert.AssertRect(result, testCase.Expected);
        }
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlRect)]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testEnclosePoints")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testEnclosePointsRepeatedInput")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testEnclosePointsWithClipping")]
    public async Task SDLEnclosePoints_Should_Return_Expected_Rect_For_Deterministic_Point_Cases()
    {
        SDL_Point[] points =
        [
            Point(3, 4),
            Point(-2, 5),
            Point(7, -1),
            Point(3, 4),
        ];

        (SDL_bool result, SDL_Rect enclosing) = EnclosePoints(points, clip: null);
        SDL_bool resultWithoutOutput = EnclosePointsWithoutOutput(points, clip: null);

        await SdlAssert.True(result, "SDL_EnclosePoints should enclose unclipped points");
        await SdlAssert.True(resultWithoutOutput, "SDL_EnclosePoints should report unclipped points without writing a result");
        await SdlAssert.AssertRect(enclosing, Rect(-2, -1, 10, 7));

        SDL_Point[] clippedPoints =
        [
            Point(0, 0),
            Point(11, 11),
            Point(5, 3),
            Point(2, 8),
        ];

        (SDL_bool clippedResult, SDL_Rect clippedEnclosing) = EnclosePoints(clippedPoints, Rect(0, 0, 10, 10));
        SDL_bool clippedResultWithoutOutput = EnclosePointsWithoutOutput(clippedPoints, Rect(0, 0, 10, 10));

        await SdlAssert.True(clippedResult, "SDL_EnclosePoints should enclose points inside the clipping rectangle");
        await SdlAssert.True(clippedResultWithoutOutput, "SDL_EnclosePoints should report clipped points without writing a result");
        await SdlAssert.AssertRect(clippedEnclosing, Rect(0, 0, 6, 9));

        (SDL_bool emptyClipResult, _) = EnclosePoints(clippedPoints, Rect(0, 0, 0, 0));
        await Assert.That(emptyClipResult).IsEqualTo(SDL_bool.SDL_FALSE);
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlRect)]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testIntersectRectAndLine")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testIntersectRectAndLineInside")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testIntersectRectAndLineOutside")]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testIntersectRectAndLineEmpty")]
    public async Task SDLIntersectRectAndLine_Should_Clip_Or_Preserve_Deterministic_Line_Cases()
    {
        LineCase[] cases =
        [
            new(Rect(0, 0, 32, 32), -32, 15, 64, 15, SDL_bool.SDL_TRUE, 0, 15, 31, 15),
            new(Rect(0, 0, 32, 32), 15, -32, 15, 64, SDL_bool.SDL_TRUE, 15, 0, 15, 31),
            new(Rect(0, 0, 32, 32), -32, -32, 64, 64, SDL_bool.SDL_TRUE, 0, 0, 31, 31),
            new(Rect(0, 0, 32, 32), 64, 64, -32, -32, SDL_bool.SDL_TRUE, 31, 31, 0, 0),
            new(Rect(0, 0, 32, 32), -1, 32, 32, -1, SDL_bool.SDL_TRUE, 0, 31, 31, 0),
            new(Rect(0, 0, 32, 32), 32, -1, -1, 32, SDL_bool.SDL_TRUE, 31, 0, 0, 31),
            new(Rect(0, 0, 32, 32), 1, 2, 30, 31, SDL_bool.SDL_TRUE, 1, 2, 30, 31),
            new(Rect(0, 0, 32, 32), -1, 0, -1, 31, SDL_bool.SDL_FALSE, -1, 0, -1, 31),
            new(Rect(0, 0, 32, 32), 0, -1, 31, -1, SDL_bool.SDL_FALSE, 0, -1, 31, -1),
            new(Rect(10, 10, 0, 0), 10, 10, 20, 20, SDL_bool.SDL_FALSE, 10, 10, 20, 20),
        ];

        foreach (LineCase testCase in cases)
        {
            LineResult result = IntersectRectAndLine(testCase.Rect, testCase.X1, testCase.Y1, testCase.X2, testCase.Y2);

            await Assert.That(result.Intersection).IsEqualTo(testCase.ExpectedIntersection);
            await Assert.That(result.Rect).IsEqualTo(testCase.Rect);
            await Assert.That(result.X1).IsEqualTo(testCase.ExpectedX1);
            await Assert.That(result.Y1).IsEqualTo(testCase.ExpectedY1);
            await Assert.That(result.X2).IsEqualTo(testCase.ExpectedX2);
            await Assert.That(result.Y2).IsEqualTo(testCase.ExpectedY2);
        }
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlRect)]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testHasIntersectionF")]
    public async Task SDLHasIntersectionF_Should_Return_Expected_Result_For_Upstream_Float_Cases()
    {
        FRectBoolCase[] cases =
        [
            new(FRect(0, 0, 0, 0), FRect(0, 0, 0, 0), SDL_bool.SDL_FALSE),
            new(FRect(0, 0, -200, 200), FRect(0, 0, -200, 200), SDL_bool.SDL_FALSE),
            new(FRect(0, 0, 10, 10), FRect(-5, 5, 10, 2), SDL_bool.SDL_TRUE),
            new(FRect(0, 0, 10, 10), FRect(-5, -5, 10, 2), SDL_bool.SDL_FALSE),
            new(FRect(0, 0, 10, 10), FRect(-5, -5, 2, 10), SDL_bool.SDL_FALSE),
            new(FRect(0, 0, 10, 10), FRect(-5, -5, 5, 5), SDL_bool.SDL_FALSE),
            new(FRect(0, 0, 10, 10), FRect(-5, -5, 5.1f, 5.1f), SDL_bool.SDL_TRUE),
            new(FRect(0, 0, 10, 10), FRect(-4.99f, -4.99f, 5, 5), SDL_bool.SDL_TRUE),
        ];

        foreach (FRectBoolCase testCase in cases)
        {
            SDL_bool result = HasIntersectionF(testCase.A, testCase.B);

            await Assert.That(result).IsEqualTo(testCase.Expected);
        }
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlRect)]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testIntersectFRect")]
    public async Task SDLIntersectFRect_Should_Return_Expected_Intersection_For_Upstream_Float_Cases()
    {
        FRectIntersectionCase[] cases =
        [
            new(FRect(0, 0, 0, 0), FRect(0, 0, 0, 0), SDL_bool.SDL_FALSE, null),
            new(FRect(0, 0, -200, 200), FRect(0, 0, -200, 200), SDL_bool.SDL_FALSE, null),
            new(FRect(0, 0, 10, 10), FRect(-5, 5, 9.9f, 2), SDL_bool.SDL_TRUE, FRect(0, 5, 4.9f, 2)),
            new(FRect(0, 0, 10, 10), FRect(-5, -5, 10, 2), SDL_bool.SDL_FALSE, null),
            new(FRect(0, 0, 10, 10), FRect(-5, -5, 2, 10), SDL_bool.SDL_FALSE, null),
            new(FRect(0, 0, 10, 10), FRect(-5, -5, 5, 5), SDL_bool.SDL_FALSE, null),
            new(FRect(0, 0, 10, 10), FRect(-5, -5, 5.5f, 6), SDL_bool.SDL_TRUE, FRect(0, 0, 0.5f, 1)),
        ];

        foreach (FRectIntersectionCase testCase in cases)
        {
            (SDL_bool result, SDL_FRect intersection) = IntersectFRect(testCase.A, testCase.B);

            await Assert.That(result).IsEqualTo(testCase.ExpectedResult);
            if (testCase.ExpectedIntersection is { } expectedIntersection)
            {
                await SdlAssert.AssertFRect(intersection, expectedIntersection);
            }
        }
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlRect)]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testUnionFRect")]
    public async Task SDLUnionFRect_Should_Write_Expected_Union_For_Upstream_Float_Cases()
    {
        FRectResultCase[] cases =
        [
            new(FRect(0, 0, 10, 10), FRect(19.9f, 20, 10, 10), FRect(0, 0, 29.9f, 30)),
            new(FRect(0, 0, 0, 0), FRect(20, 20.1f, 10.1f, 10), FRect(20, 20.1f, 10.1f, 10)),
            new(FRect(-200, -4.5f, 450, 33), FRect(20, 20, 10, 10), FRect(-200, -4.5f, 450, 34.5f)),
            new(FRect(0, 0, 15, 16.5f), FRect(20, 20, 0, 0), FRect(0, 0, 15, 16.5f)),
        ];

        foreach (FRectResultCase testCase in cases)
        {
            SDL_FRect result = UnionFRect(testCase.A, testCase.B);

            await SdlAssert.AssertFRect(result, testCase.Expected);
        }
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlRect)]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testEncloseFPoints")]
    public async Task SDLEncloseFPoints_Should_Return_Expected_Rect_For_Upstream_Float_Cases()
    {
        SDL_FPoint[] points =
        [
            FPoint(0.5f, 0.1f),
            FPoint(5.5f, 7.1f),
            FPoint(1.5f, 1.1f),
        ];

        FEncloseCase[] cases =
        [
            new(FRect(0, 0, 10, 10), SDL_bool.SDL_TRUE, FRect(0.5f, 0.1f, 6, 8)),
            new(FRect(1.2f, 1, 10, 10), SDL_bool.SDL_TRUE, FRect(1.5f, 1.1f, 5, 7)),
            new(FRect(-10, -10, 3, 3), SDL_bool.SDL_FALSE, null),
            new(null, SDL_bool.SDL_TRUE, FRect(0.5f, 0.1f, 6, 8)),
        ];

        foreach (FEncloseCase testCase in cases)
        {
            (SDL_bool result, SDL_FRect enclosing) = EncloseFPoints(points, testCase.Clip);

            await Assert.That(result).IsEqualTo(testCase.ExpectedResult);
            if (testCase.ExpectedEnclosing is { } expectedEnclosing)
            {
                await SdlAssert.AssertFRect(enclosing, expectedEnclosing);
            }
        }
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlRect)]
    [UpstreamSdlTest("test/testautomation_rect.c", "rect_testIntersectFRectAndLine")]
    public async Task SDLIntersectFRectAndLine_Should_Clip_Or_Preserve_Upstream_Float_Line_Cases()
    {
        FLineCase[] cases =
        [
            new(FRect(0, 0, 0, 0), FPoint(-4.8f, -4.8f), FPoint(5.2f, 5.2f), SDL_bool.SDL_FALSE, FPoint(-4.8f, -4.8f), FPoint(5.2f, 5.2f)),
            new(FRect(0, 0, 2, 2), FPoint(-1, -1), FPoint(3.5f, 3.5f), SDL_bool.SDL_TRUE, FPoint(0, 0), FPoint(1, 1)),
            new(FRect(-4, -4, 14, 14), FPoint(8, 22), FPoint(8, 33), SDL_bool.SDL_FALSE, FPoint(8, 22), FPoint(8, 33)),
        ];

        foreach (FLineCase testCase in cases)
        {
            FLineResult result = IntersectFRectAndLine(testCase.Rect, testCase.P1, testCase.P2);

            await Assert.That(result.Intersection).IsEqualTo(testCase.ExpectedIntersection);
            await SdlAssert.AssertFPoint(result.P1, testCase.ExpectedP1);
            await SdlAssert.AssertFPoint(result.P2, testCase.ExpectedP2);
        }
    }

    private static SDL_Rect Rect(int x, int y, int w, int h)
    {
        return new SDL_Rect { x = x, y = y, w = w, h = h };
    }

    private static SDL_FRect FRect(float x, float y, float w, float h)
    {
        return new SDL_FRect { x = x, y = y, w = w, h = h };
    }

    private static SDL_Point Point(int x, int y)
    {
        return new SDL_Point { x = x, y = y };
    }

    private static SDL_FPoint FPoint(float x, float y)
    {
        return new SDL_FPoint { x = x, y = y };
    }

    private static unsafe SDL_bool HasIntersection(SDL_Rect a, SDL_Rect b)
    {
        SDL_Rect originalA = a;
        SDL_Rect originalB = b;
        SDL_bool result = SDL_HasIntersection(&a, &b);

        EnsureRectUnchanged(a, originalA, nameof(a));
        EnsureRectUnchanged(b, originalB, nameof(b));
        return result;
    }

    private static unsafe (SDL_bool Result, SDL_Rect Intersection) IntersectRect(SDL_Rect a, SDL_Rect b)
    {
        SDL_Rect originalA = a;
        SDL_Rect originalB = b;
        SDL_Rect intersection = Rect(-101, -102, 103, 104);
        SDL_bool result = SDL_IntersectRect(&a, &b, &intersection);

        EnsureRectUnchanged(a, originalA, nameof(a));
        EnsureRectUnchanged(b, originalB, nameof(b));
        return (result, intersection);
    }

    private static unsafe SDL_Rect UnionRect(SDL_Rect a, SDL_Rect b)
    {
        SDL_Rect originalA = a;
        SDL_Rect originalB = b;
        SDL_Rect result = default;
        SDL_UnionRect(&a, &b, &result);

        EnsureRectUnchanged(a, originalA, nameof(a));
        EnsureRectUnchanged(b, originalB, nameof(b));
        return result;
    }

    private static unsafe (SDL_bool Result, SDL_Rect Enclosing) EnclosePoints(SDL_Point[] points, SDL_Rect? clip)
    {
        SDL_Point[] originalPoints = (SDL_Point[])points.Clone();
        fixed (SDL_Point* pointsPointer = points)
        {
            SDL_Rect result = default;
            if (clip is { } clipValue)
            {
                SDL_Rect originalClip = clipValue;
                SDL_bool clippedResult = SDL_EnclosePoints(pointsPointer, points.Length, &clipValue, &result);

                EnsurePointsUnchanged(points, originalPoints, nameof(points));
                EnsureRectUnchanged(clipValue, originalClip, nameof(clip));
                return (clippedResult, result);
            }

            SDL_bool unclippedResult = SDL_EnclosePoints(pointsPointer, points.Length, null, &result);

            EnsurePointsUnchanged(points, originalPoints, nameof(points));
            return (unclippedResult, result);
        }
    }

    private static unsafe SDL_bool EnclosePointsWithoutOutput(SDL_Point[] points, SDL_Rect? clip)
    {
        SDL_Point[] originalPoints = (SDL_Point[])points.Clone();
        fixed (SDL_Point* pointsPointer = points)
        {
            if (clip is { } clipValue)
            {
                SDL_Rect originalClip = clipValue;
                SDL_bool result = SDL_EnclosePoints(pointsPointer, points.Length, &clipValue, null);

                EnsurePointsUnchanged(points, originalPoints, nameof(points));
                EnsureRectUnchanged(clipValue, originalClip, nameof(clip));
                return result;
            }

            SDL_bool unclippedResult = SDL_EnclosePoints(pointsPointer, points.Length, null, null);

            EnsurePointsUnchanged(points, originalPoints, nameof(points));
            return unclippedResult;
        }
    }

    private static unsafe LineResult IntersectRectAndLine(SDL_Rect rect, int x1, int y1, int x2, int y2)
    {
        SDL_Rect rectCopy = rect;
        SDL_bool result = SDL_IntersectRectAndLine(&rectCopy, &x1, &y1, &x2, &y2);

        return new LineResult(result, rectCopy, x1, y1, x2, y2);
    }

    private static unsafe SDL_bool HasIntersectionF(SDL_FRect a, SDL_FRect b)
    {
        SDL_FRect originalA = a;
        SDL_FRect originalB = b;
        SDL_bool result = SDL_HasIntersectionF(&a, &b);

        EnsureFRectUnchanged(a, originalA, nameof(a));
        EnsureFRectUnchanged(b, originalB, nameof(b));
        return result;
    }

    private static unsafe (SDL_bool Result, SDL_FRect Intersection) IntersectFRect(SDL_FRect a, SDL_FRect b)
    {
        SDL_FRect originalA = a;
        SDL_FRect originalB = b;
        SDL_FRect intersection = default;
        SDL_bool result = SDL_IntersectFRect(&a, &b, &intersection);

        EnsureFRectUnchanged(a, originalA, nameof(a));
        EnsureFRectUnchanged(b, originalB, nameof(b));
        return (result, intersection);
    }

    private static unsafe SDL_FRect UnionFRect(SDL_FRect a, SDL_FRect b)
    {
        SDL_FRect originalA = a;
        SDL_FRect originalB = b;
        SDL_FRect result = default;
        SDL_UnionFRect(&a, &b, &result);

        EnsureFRectUnchanged(a, originalA, nameof(a));
        EnsureFRectUnchanged(b, originalB, nameof(b));
        return result;
    }

    private static unsafe (SDL_bool Result, SDL_FRect Enclosing) EncloseFPoints(SDL_FPoint[] points, SDL_FRect? clip)
    {
        SDL_FPoint[] originalPoints = (SDL_FPoint[])points.Clone();
        fixed (SDL_FPoint* pointsPointer = points)
        {
            SDL_FRect result = default;
            if (clip is { } clipValue)
            {
                SDL_FRect originalClip = clipValue;
                SDL_bool clippedResult = SDL_EncloseFPoints(pointsPointer, points.Length, &clipValue, &result);

                EnsureFPointsUnchanged(points, originalPoints, nameof(points));
                EnsureFRectUnchanged(clipValue, originalClip, nameof(clip));
                return (clippedResult, result);
            }

            SDL_bool unclippedResult = SDL_EncloseFPoints(pointsPointer, points.Length, null, &result);

            EnsureFPointsUnchanged(points, originalPoints, nameof(points));
            return (unclippedResult, result);
        }
    }

    private static unsafe FLineResult IntersectFRectAndLine(SDL_FRect rect, SDL_FPoint p1, SDL_FPoint p2)
    {
        SDL_FRect originalRect = rect;
        SDL_bool result = SDL_IntersectFRectAndLine(&rect, &p1.x, &p1.y, &p2.x, &p2.y);

        EnsureFRectUnchanged(rect, originalRect, nameof(rect));
        return new FLineResult(result, p1, p2);
    }

    private static void EnsureRectUnchanged(SDL_Rect actual, SDL_Rect expected, string parameterName)
    {
        if (!RectEquals(actual, expected))
        {
            throw new InvalidOperationException($"SDL unexpectedly modified {parameterName}.");
        }
    }

    private static void EnsureFRectUnchanged(SDL_FRect actual, SDL_FRect expected, string parameterName)
    {
        if (!FRectEquals(actual, expected))
        {
            throw new InvalidOperationException($"SDL unexpectedly modified {parameterName}.");
        }
    }

    private static void EnsurePointsUnchanged(SDL_Point[] actual, SDL_Point[] expected, string parameterName)
    {
        for (int i = 0; i < actual.Length; i++)
        {
            if (actual[i].x != expected[i].x || actual[i].y != expected[i].y)
            {
                throw new InvalidOperationException($"SDL unexpectedly modified {parameterName}[{i}].");
            }
        }
    }

    private static void EnsureFPointsUnchanged(SDL_FPoint[] actual, SDL_FPoint[] expected, string parameterName)
    {
        for (int i = 0; i < actual.Length; i++)
        {
            if (!ApproximatelyEqual(actual[i].x, expected[i].x) || !ApproximatelyEqual(actual[i].y, expected[i].y))
            {
                throw new InvalidOperationException($"SDL unexpectedly modified {parameterName}[{i}].");
            }
        }
    }

    private static bool RectEquals(SDL_Rect actual, SDL_Rect expected)
    {
        return actual.x == expected.x
               && actual.y == expected.y
               && actual.w == expected.w
               && actual.h == expected.h;
    }

    private static bool RectIsEmpty(SDL_Rect rect)
    {
        return rect.w <= 0 || rect.h <= 0;
    }

    private static bool FRectEquals(SDL_FRect actual, SDL_FRect expected)
    {
        return ApproximatelyEqual(actual.x, expected.x)
               && ApproximatelyEqual(actual.y, expected.y)
               && ApproximatelyEqual(actual.w, expected.w)
               && ApproximatelyEqual(actual.h, expected.h);
    }

    private readonly record struct RectBoolCase(SDL_Rect A, SDL_Rect B, SDL_bool Expected);

    private readonly record struct RectIntersectionCase(SDL_Rect A, SDL_Rect B, SDL_bool ExpectedResult, SDL_Rect? ExpectedIntersection);

    private readonly record struct RectResultCase(SDL_Rect A, SDL_Rect B, SDL_Rect Expected);

    private readonly record struct LineCase(
        SDL_Rect Rect,
        int X1,
        int Y1,
        int X2,
        int Y2,
        SDL_bool ExpectedIntersection,
        int ExpectedX1,
        int ExpectedY1,
        int ExpectedX2,
        int ExpectedY2);

    private readonly record struct LineResult(SDL_bool Intersection, SDL_Rect Rect, int X1, int Y1, int X2, int Y2);

    private readonly record struct FRectBoolCase(SDL_FRect A, SDL_FRect B, SDL_bool Expected);

    private readonly record struct FRectIntersectionCase(SDL_FRect A, SDL_FRect B, SDL_bool ExpectedResult, SDL_FRect? ExpectedIntersection);

    private readonly record struct FRectResultCase(SDL_FRect A, SDL_FRect B, SDL_FRect Expected);

    private readonly record struct FEncloseCase(SDL_FRect? Clip, SDL_bool ExpectedResult, SDL_FRect? ExpectedEnclosing);

    private readonly record struct FLineCase(SDL_FRect Rect, SDL_FPoint P1, SDL_FPoint P2, SDL_bool ExpectedIntersection, SDL_FPoint ExpectedP1, SDL_FPoint ExpectedP2);

    private readonly record struct FLineResult(SDL_bool Intersection, SDL_FPoint P1, SDL_FPoint P2);
}
