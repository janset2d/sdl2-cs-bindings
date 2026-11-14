using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.PostProcess;

public sealed class StripVarargsRewriterTests
{
    [Test]
    public async Task VisitMethodDeclaration_Should_Remove_Varargs_And_Preserve_The_Fixed_Prefix()
    {
        var source = PostProcessFixture.Source("varargsSource");
        var output = PostProcessFixture.Rewrite(source, new StripVarargsRewriter());
        await Assert.That(output).IsEqualTo(PostProcessFixture.Source("varargsExpected"));
    }

    [Test]
    public async Task VisitMethodDeclaration_Should_Preserve_Empty_Nonmatching_Trivia_And_Repeated_Output()
    {
        var rewriter = new StripVarargsRewriter();
        await Assert.That(PostProcessFixture.Rewrite("", rewriter)).IsEqualTo("");
        var nonmatching = PostProcessFixture.Source("nonmatching");
        await Assert.That(PostProcessFixture.Rewrite(nonmatching, rewriter)).IsEqualTo(nonmatching);
        var output = PostProcessFixture.Rewrite(PostProcessFixture.Source("varargsTrivia"), rewriter);
        await Assert.That(output).IsEqualTo(PostProcessFixture.Source("varargsTriviaExpected"));
        rewriter.Reset();
        await Assert.That(PostProcessFixture.Rewrite(output, rewriter)).IsEqualTo(output);
    }
}
