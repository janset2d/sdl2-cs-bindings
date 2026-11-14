using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.PostProcess;

public sealed class GuidSubstitutionRewriterTests
{
    [Test]
    public async Task VisitCompilationUnit_Should_Substitute_Guid_And_Remove_The_Original_Struct()
    {
        var source = PostProcessFixture.Source("guidSource");
        var output = PostProcessFixture.Rewrite(source, new GuidSubstitutionRewriter());
        await Assert.That(output).Contains("using System;\n");
        await Assert.That(output).Contains("public static Guid Get(Guid value)");
        await Assert.That(output).DoesNotContain("SDL_GUID");
        await Assert.That(output).DoesNotContain("partial struct");
    }

    [Test]
    public async Task VisitCompilationUnit_Should_Preserve_Empty_Nonmatching_Trivia_And_Repeated_Output()
    {
        var rewriter = new GuidSubstitutionRewriter();
        await Assert.That(PostProcessFixture.Rewrite("", rewriter)).IsEqualTo("");
        var nonmatching = PostProcessFixture.Source("nonmatching");
        await Assert.That(PostProcessFixture.Rewrite(nonmatching, rewriter)).IsEqualTo(nonmatching);
        var output = PostProcessFixture.Rewrite(PostProcessFixture.Source("guidTrivia"), rewriter);
        await Assert.That(output).IsEqualTo(PostProcessFixture.Source("guidTriviaExpected"));
        rewriter.Reset();
        await Assert.That(PostProcessFixture.Rewrite(output, rewriter)).IsEqualTo(output);
    }
}
