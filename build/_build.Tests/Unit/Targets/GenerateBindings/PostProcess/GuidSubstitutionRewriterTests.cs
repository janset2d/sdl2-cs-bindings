using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.PostProcess;

public sealed class GuidSubstitutionRewriterTests
{
    [Test]
    public async Task VisitCompilationUnit_Should_Substitute_Guid_And_Remove_The_Original_Struct()
    {
        const string source = "namespace SDL2; public partial struct SDL_GUID { } class Native { public static SDL_GUID Get(SDL_GUID value) => value; }";
        var output = PostProcessFixture.Rewrite(source, new GuidSubstitutionRewriter());
        await Assert.That(output).Contains("using System;\n");
        await Assert.That(output).Contains("public static Guid Get(Guid value)");
        await Assert.That(output).DoesNotContain("SDL_GUID");
        await Assert.That(output).DoesNotContain("partial struct");
    }
}
