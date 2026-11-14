using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.PostProcess;

public sealed class StructLayoutSequentialRewriterTests
{
    [Test]
    public async Task VisitCompilationUnit_Should_Decorate_Bare_Structs_Without_Conflicting_Layouts()
    {
        var bare = Rewrite("barePod");
        await Assert.That(bare).Contains("using System.Runtime.InteropServices;");
        await Assert.That(bare).Contains("    [StructLayout(LayoutKind.Sequential)]\n    public partial struct SDL_Rect");
        var explicitLayout = Rewrite("explicitLayout");
        await Assert.That(Count(explicitLayout, "LayoutKind.")).IsEqualTo(1);
        await Assert.That(explicitLayout).DoesNotContain("LayoutKind.Sequential");
        var sequential = Rewrite("existingSequential");
        await Assert.That(Count(sequential, "StructLayout(LayoutKind.Sequential)")).IsEqualTo(1);
        var partial = Rewrite("partialGuard");
        await Assert.That(Count(partial, "LayoutKind.")).IsEqualTo(1);
        await Assert.That(partial).DoesNotContain("LayoutKind.Sequential");
        var nested = Rewrite("nestedBare");
        await Assert.That(Count(nested, "StructLayout(LayoutKind.Sequential)")).IsEqualTo(2);
        var explicitOuter = Rewrite("explicitOuterNestedBare");
        await Assert.That(Count(explicitOuter, "LayoutKind.")).IsEqualTo(2);
        await Assert.That(explicitOuter).Contains("LayoutKind.Explicit");
        await Assert.That(explicitOuter).Contains("LayoutKind.Sequential");
        var sameName = Rewrite("sameSimpleNameNested");
        await Assert.That(Count(sameName, "LayoutKind.")).IsEqualTo(3);
        await Assert.That(Count(sameName, "LayoutKind.Sequential")).IsEqualTo(2);
    }

    [Test]
    public async Task VisitCompilationUnit_Should_Preserve_Empty_Nonmatching_Trivia_And_Repeated_Output()
    {
        var rewriter = new StructLayoutSequentialRewriter();
        await Assert.That(PostProcessFixture.Rewrite("", rewriter)).IsEqualTo("");
        var nonmatching = PostProcessFixture.Source("nonmatching");
        await Assert.That(PostProcessFixture.Rewrite(nonmatching, rewriter)).IsEqualTo(nonmatching);
        var output = PostProcessFixture.Rewrite(PostProcessFixture.Source("layoutTrivia"), rewriter);
        await Assert.That(output).Contains("// keep the struct comment\n[StructLayout(LayoutKind.Sequential)]\npublic struct SDL_Rect");
        await Assert.That(output).Contains("public int Value; } // keep the tail");
        rewriter.Reset();
        await Assert.That(PostProcessFixture.Rewrite(output, rewriter)).IsEqualTo(output);
    }

    private static string Rewrite(string fixture)
        => PostProcessFixture.Rewrite(PostProcessFixture.Source(fixture), new StructLayoutSequentialRewriter());

    private static int Count(string source, string text) => source.Split(text, StringSplitOptions.None).Length - 1;
}
