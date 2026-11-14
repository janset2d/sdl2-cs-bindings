using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.PostProcess;

public sealed class StripVarargsRewriterTests
{
    [Test]
    public async Task VisitMethodDeclaration_Should_Remove_Varargs_And_Preserve_The_Fixed_Prefix()
    {
        const string source = "class Native { public static extern void SDL_Log(byte* fmt, __arglist); }";
        var output = PostProcessFixture.Rewrite(source, new StripVarargsRewriter());
        await Assert.That(output).IsEqualTo("class Native { public static extern void SDL_Log(byte* fmt); }");
    }
}
