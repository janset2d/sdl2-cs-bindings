using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.ModelBuilding.Declarations;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Declarations;

public sealed class BindableDeclarationPolicyTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task IsBindableFunction_Should_Require_Sdl2_Source_Export_Name()
    {
        var policy = CreatePolicy();
        var valid = SdlFunction("SDL_GetTicks", "SDL_timer.h");
        var inline = SdlFunction("SDL_RectEmpty", "SDL_rect.h");
        inline.Flags = CppFunctionFlags.Inline;
        var excluded = SdlFunction("SDL_main", "SDL_main.h");
        var nonSdl = new CppFunction("SDL_GetTicks")
        {
            ReturnType = CppPrimitiveType.Int,
            Span = HeaderSpan("C:/vendor/include/not-sdl/SDL_timer.h"),
        };

        await Assert.That(policy.IsBindableFunction(valid)).IsTrue();
        await Assert.That(policy.IsBindableFunction(inline)).IsFalse();
        await Assert.That(policy.IsBindableFunction(excluded)).IsFalse();
        await Assert.That(policy.IsBindableFunction(nonSdl)).IsFalse();
    }

    private static BindableDeclarationPolicy CreatePolicy() =>
        new(DefaultConfig, new KnownUnsupportedDeclarationPolicy(DefaultConfig));

    private static CppFunction SdlFunction(string name, string headerName) =>
        new(name)
        {
            ReturnType = CppPrimitiveType.Int,
            Span = SdlHeaderSpan(headerName),
        };

    private static CppSourceSpan SdlHeaderSpan(string headerName) =>
        HeaderSpan($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}");

    private static CppSourceSpan HeaderSpan(string sourceFile) =>
        new(
            new CppSourceLocation(sourceFile, 0, 1, 1),
            new CppSourceLocation(sourceFile, 1, 1, 2));
}
