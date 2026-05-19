using Build.Targets.GenerateBindings.Parsing;
using Build.Targets.GenerateBindings.Translation;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class MacroCandidateCollectorTests
{
    [Test]
    public async Task Collect_Should_Return_Macros_With_View_And_Source_Evidence()
    {
        var compilation = new CppCompilation();
        compilation.Macros.Add(Macro("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\"", "SDL_hints.h"));

        var result = new CppAstParseResult(ParseView("Neutral"), [compilation]);

        var candidates = MacroCandidateCollector.Collect([result]);

        await Assert.That(candidates.Count).IsEqualTo(1);
        await Assert.That(candidates[0].Name).IsEqualTo("SDL_HINT_RENDER_DRIVER");
        await Assert.That(candidates[0].Value).IsEqualTo("\"SDL_RENDER_DRIVER\"");
        await Assert.That(candidates[0].SourceHeader).IsEqualTo("SDL_hints.h");
        await Assert.That(candidates[0].ParseViewName).IsEqualTo("Neutral");
    }

    [Test]
    public async Task Collect_Should_Preserve_Function_Like_Parameters()
    {
        var compilation = new CppCompilation();
        var macro = Macro("SDL_BUTTON", "(1u << ((X) - 1))", "SDL_mouse.h");
        macro.Parameters = ["X"];
        compilation.Macros.Add(macro);

        var candidates = MacroCandidateCollector.Collect([new CppAstParseResult(ParseView("Neutral"), [compilation])]);

        await Assert.That(candidates.Single().IsFunctionLike).IsTrue();
        await Assert.That(candidates.Single().Parameters).IsEquivalentTo(["X"]);
    }

    private static PlatformParseView ParseView(string name) =>
        new(name, PlatformConditionKind.Neutral, SupportedOsPlatform: null, Defines: [], Undefines: []);

    private static CppMacro Macro(string name, string value, string header)
    {
        var macro = new CppMacro(name) { Value = value };
        macro.Span = new CppSourceSpan(
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{header}", 0, 1, 1),
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{header}", 1, 1, 2));
        macro.Tokens.Add(new CppToken(CppTokenKind.Literal, value));
        return macro;
    }
}
