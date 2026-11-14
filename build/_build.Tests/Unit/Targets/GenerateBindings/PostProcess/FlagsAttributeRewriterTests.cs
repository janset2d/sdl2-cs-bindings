using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Build.Tests.Unit.Targets.GenerateBindings.PostProcess;

public sealed class FlagsAttributeRewriterTests
{
    [Test]
    public async Task VisitEnumDeclaration_Should_Decorate_Only_Allowlisted_Or_Suffixed_Enums_Once()
    {
        var config = await PostProcessFixture.ConfigAsync(PostProcessFixture.World());
        var allowList = config.FlagsAllowList("core");
        await CheckEnumAsync("IMG_InitFlags", PostProcessFixture.Source("imageFlagsMembers"), new HashSet<string>([], StringComparer.Ordinal), true);
        await CheckEnumAsync("SDL_Keymod", PostProcessFixture.Source("keymodMembers"), allowList, true);
        await CheckEnumAsync("SDL_bool", PostProcessFixture.Source("boolMembers"), allowList, false);
        await CheckEnumAsync("SDL_HitTestResult", PostProcessFixture.Source("hitTestMembers"), allowList, false);
        await CheckEnumAsync("SDL_someflags", PostProcessFixture.Source("lowercaseFlagsMembers"), allowList, false);
        var prefixed = PostProcessFixture.Rewrite(PostProcessFixture.Source("prefixedInput"), new FlagsAttributeRewriter(allowList));
        var root = await CSharpSyntaxTree.ParseText(prefixed, CSharpParseOptions.Default).GetRootAsync();
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>().Select(attribute => attribute.Name.ToString()).ToArray();
        await Assert.That(string.Join(',', attributes)).IsEqualTo("System.Flags");
        var separated = PostProcessFixture.Rewrite(PostProcessFixture.Source("separatedInput"), new FlagsAttributeRewriter(allowList));
        await Assert.That(separated).Contains("[System.Flags]\n    public enum SDL_TextureModulate");
        var imageAllowList = config.FlagsAllowList("image");
        await Assert.That(imageAllowList.Count).IsEqualTo(0);
        await CheckEnumAsync("IMG_InitFlags", PostProcessFixture.Source("imageFlagsMembers"), imageAllowList, true);
    }

    [Test]
    public async Task VisitEnumDeclaration_Should_Preserve_Empty_Nonmatching_Trivia_And_Repeated_Configured_Output()
    {
        var config = await PostProcessFixture.ConfigAsync(PostProcessFixture.World());
        var rewriter = new FlagsAttributeRewriter(config.FlagsAllowList("core"));
        await Assert.That(PostProcessFixture.Rewrite("", rewriter)).IsEqualTo("");
        var nonmatching = PostProcessFixture.Source("nonmatching");
        await Assert.That(PostProcessFixture.Rewrite(nonmatching, rewriter)).IsEqualTo(nonmatching);
        var source = PostProcessFixture.Source("flagsTrivia");
        await Assert.That(PostProcessFixture.Rewrite(source, new FlagsAttributeRewriter(config.FlagsAllowList("image")))).IsEqualTo(source);
        var output = PostProcessFixture.Rewrite(source, rewriter);
        await Assert.That(output).Contains("// keep the enum comment\n[System.Flags]\npublic enum SDL_Keymod");
        await Assert.That(output).Contains("Value = 1 } // keep the tail");
        rewriter.Reset();
        await Assert.That(PostProcessFixture.Rewrite(output, rewriter)).IsEqualTo(output);
    }

    private static async Task CheckEnumAsync(string name, string members, HashSet<string> allowList, bool flags)
    {
        var source = $"namespace SDL2 {{ public enum {name} {{ {members} }} }}";
        var output = PostProcessFixture.Rewrite(source, new FlagsAttributeRewriter(allowList));
        var root = await CSharpSyntaxTree.ParseText(output, CSharpParseOptions.Default).GetRootAsync();
        var declaration = root.DescendantNodes().OfType<EnumDeclarationSyntax>().Single();
        await Assert.That(declaration.Identifier.ValueText).IsEqualTo(name);
        await Assert.That(string.Join(',', declaration.AttributeLists.SelectMany(list => list.Attributes).Select(attribute => attribute.Name.ToString())))
            .IsEqualTo(flags ? "System.Flags" : "");
    }
}
