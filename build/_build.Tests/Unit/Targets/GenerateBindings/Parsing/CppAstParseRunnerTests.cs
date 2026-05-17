using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Parsing;
using Build.Tests.Fixtures;
using static Build.Tests.Fixtures.BindingGenerationFixture;

namespace Build.Tests.Unit.Targets.GenerateBindings.Parsing;

public sealed class CppAstParseRunnerTests
{
    [Test]
    public async Task CreateOptions_Should_Use_Preprocessor_Macro_Switching()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_video.h", "/* header */");
        var resolver = new HeaderSetResolver(world.CakeContext);
        var config = Sdl2CoreConfig();
        var headerSet = resolver.Resolve(
            config,
            world.RepoRoot.Combine("vcpkg_installed"),
            world.RepoRoot.Combine("synthetic-headers"),
            "x64-linux-hybrid");
        var linux = PlatformCatalog.CreateSdl2Catalog().ParseViews.Single(view => view.Name == "Linux");

        var options = CppAstParseRunner.CreateOptions(config, headerSet, linux);

        // Platform separation is preprocessor-driven (Defines + Undefines). TargetSystem
        // is pinned to "linux" because the binding-generator container is Linux-canonical;
        // CppAst's default ("windows") would trigger SDL's _MSC_VER header branches.
        // Manifest-driven config (BindingGenerationFixture.Sdl2CoreConfig) supplies
        // ParseDefines + ClangArgs; PlatformParseView supplies the per-view macro group.
        await Assert.That(options.ParseMacros).IsTrue();
        await Assert.That(options.ParserKind).IsEqualTo(CppAst.CppParserKind.C);
        await Assert.That(options.TargetSystem).IsEqualTo("linux");
        await Assert.That(options.SystemIncludeFolders).Contains(headerSet.IncludeRoot.FullPath);
        await Assert.That(options.Defines).Contains("SDL_DECLSPEC=");
        await Assert.That(options.Defines).Contains("SDL_DISABLE_IMMINTRIN_H=1");
        await Assert.That(options.Defines).Contains("SDL_DISABLE_XMMINTRIN_H=1");
        await Assert.That(options.Defines).Contains("SDL_DISABLE_EMMINTRIN_H=1");
        await Assert.That(options.Defines).Contains("SDL_VIDEO_DRIVER_X11=1");
        await Assert.That(options.AdditionalArguments).Contains("-fdeclspec");
        await Assert.That(options.AdditionalArguments).Contains("-U__has_builtin");
        await Assert.That(options.AdditionalArguments).Contains("-U_WIN32");
    }
}
