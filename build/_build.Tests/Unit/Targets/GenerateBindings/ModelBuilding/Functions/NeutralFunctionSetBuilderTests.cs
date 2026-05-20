using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding.Declarations;
using Build.Targets.GenerateBindings.ModelBuilding.Functions;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using Build.Targets.GenerateBindings.Parse;
using Build.Targets.GenerateBindings.PlatformViews;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Functions;

public sealed class NeutralFunctionSetBuilderTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task Build_Should_Merge_Required_Function_Names_With_Parsed_Neutral_Functions()
    {
        var builder = CreateBuilder();
        var neutralCompilation = new CppCompilation();
        neutralCompilation.Functions.Add(SdlFunction("SDL_GetTicks", "SDL_timer.h"));

        var platformCompilation = new CppCompilation();
        platformCompilation.Functions.Add(SdlFunction("SDL_LinuxOnly", "SDL_system.h"));

        var required = new[]
        {
            new BindingFunction("SDL_Init", BindingGenerationFixture.NativeInt(), [], "SDL.h"),
        };

        var names = builder.Build(
            [
                ParseResult("Neutral", neutralCompilation),
                ParseResult("Linux", platformCompilation, "linux"),
            ],
            required);

        await Assert.That(names).IsEquivalentTo(["SDL_GetTicks", "SDL_Init"]);
    }

    private static NeutralFunctionSetBuilder CreateBuilder()
    {
        var policy = new BindableDeclarationPolicy(DefaultConfig, new KnownUnsupportedDeclarationPolicy(DefaultConfig));
        var classifier = new NativeTypeClassifier(NativeTypeClassificationContext.FromConfig(DefaultConfig));
        return new NeutralFunctionSetBuilder(new BindingFunctionTranslator(policy, classifier));
    }

    private static CppAstParseResult ParseResult(string name, CppCompilation compilation, string? platform = null) =>
        new(
            new PlatformParseView(
                Name: name,
                Kind: platform is null ? PlatformConditionKind.Neutral : PlatformConditionKind.OperatingSystem,
                SupportedOsPlatform: platform,
                Defines: [],
                Undefines: []),
            [compilation]);

    private static CppFunction SdlFunction(string name, string headerName) =>
        new(name)
        {
            ReturnType = CppPrimitiveType.Int,
            Span = SdlHeaderSpan(headerName),
        };

    private static CppSourceSpan SdlHeaderSpan(string headerName) =>
        new(
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}", 0, 1, 1),
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}", 1, 1, 2));
}
