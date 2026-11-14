using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.PostProcess;

public sealed class DllImportToLibraryImportRewriterTests
{
    [Test]
    public async Task VisitMethodDeclaration_Should_Emit_LibraryImport_And_CallConv_Metadata()
    {
        var output = PostProcessFixture.Rewrite(PostProcessFixture.Source("source"), new DllImportToLibraryImportRewriter());
        await Assert.That(output).Contains("using System.Runtime.CompilerServices;");
        await Assert.That(output).Contains("[LibraryImport(\"SDL2\")]");
        await Assert.That(output).Contains("[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]");
        await Assert.That(output).Contains("public static partial int SDL_Init(uint flags);");
        await Assert.That(output).DoesNotContain("DllImport");
        await Assert.That(output).DoesNotContain(" extern ");
    }

    [Test]
    public async Task VisitMethodDeclaration_Should_Preserve_Empty_Nonmatching_Trivia_And_Repeated_Output()
    {
        var rewriter = new DllImportToLibraryImportRewriter();
        await Assert.That(PostProcessFixture.Rewrite("", rewriter)).IsEqualTo("");
        var nonmatching = PostProcessFixture.Source("nonmatching");
        await Assert.That(PostProcessFixture.Rewrite(nonmatching, rewriter)).IsEqualTo(nonmatching);
        var output = PostProcessFixture.Rewrite(PostProcessFixture.Source("importTrivia"), rewriter);
        await Assert.That(output).IsEqualTo(PostProcessFixture.Source("importTriviaExpected"));
        rewriter.Reset();
        await Assert.That(PostProcessFixture.Rewrite(output, rewriter)).IsEqualTo(output);
    }
}
