using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Parsing;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Parsing;

public sealed class CppAstParseRunnerTests
{
    [Test]
    public async Task CreateOptions_Should_Use_Preprocessor_Macro_Switching()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_video.h", "/* header */");
        var resolver = new HeaderSetResolver(world.CakeContext);
        var headerSet = resolver.ResolveSdl2CoreHeaders(
            world.RepoRoot.Combine("vcpkg_installed"),
            world.RepoRoot.Combine("synthetic-headers"),
            "x64-linux-hybrid");
        var linux = PlatformCatalog.CreateSdl2Catalog().ParseViews.Single(view => view.Name == "Linux");
        var runner = new CppAstParseRunner(new ParseDiagnosticFormatter());

        var options = runner.CreateOptions(headerSet, linux);

        // Platform separation is preprocessor-driven (Defines + Undefines). TargetSystem
        // is pinned to "linux" because the binding-generator container is Linux-canonical;
        // CppAst's default ("windows") would trigger SDL's _MSC_VER header branches.
        // SDL_DISABLE_*_H neutralises SDL_cpuinfo.h's intrinsic-header includes that
        // collide with libclang's internal builtin table.
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
