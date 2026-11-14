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
        await CheckEnumAsync("IMG_InitFlags", "IMG_INIT_JPG = 0x00000001, IMG_INIT_PNG = 0x00000002,", new HashSet<string>([], StringComparer.Ordinal), true);
        await CheckEnumAsync("SDL_Keymod", "KMOD_NONE = 0x0000, KMOD_LSHIFT = 0x0001, KMOD_CTRL = KMOD_LCTRL | KMOD_RCTRL,", allowList, true);
        await CheckEnumAsync("SDL_bool", "SDL_FALSE = 0, SDL_TRUE = 1,", allowList, false);
        await CheckEnumAsync("SDL_HitTestResult", "SDL_HITTEST_NORMAL, SDL_HITTEST_DRAGGABLE,", allowList, false);
        await CheckEnumAsync("SDL_someflags", "VALUE_A = 0x1, VALUE_B = 0x2,", allowList, false);
        var prefixed = PostProcessFixture.Rewrite(PostProcessFixture.Source("prefixedInput"), new FlagsAttributeRewriter(allowList));
        var root = await CSharpSyntaxTree.ParseText(prefixed, CSharpParseOptions.Default).GetRootAsync();
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>().Select(attribute => attribute.Name.ToString()).ToArray();
        await Assert.That(string.Join(',', attributes)).IsEqualTo("System.Flags");
        var separated = PostProcessFixture.Rewrite(PostProcessFixture.Source("separatedInput"), new FlagsAttributeRewriter(allowList));
        await Assert.That(separated).Contains("[System.Flags]\n    public enum SDL_TextureModulate");
        var imageAllowList = config.FlagsAllowList("image");
        await Assert.That(imageAllowList.Count).IsEqualTo(0);
        await CheckEnumAsync("IMG_InitFlags", "IMG_INIT_JPG = 0x00000001, IMG_INIT_PNG = 0x00000002,", imageAllowList, true);
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
