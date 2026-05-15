using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Parsing;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Parsing;

public sealed class CppAstParseRunnerTests
{
    [Test]
    public async Task CreateOptions_Should_Use_Preprocessor_Macro_Switching_Without_Target_System()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL.h", "/* header */");
        var resolver = new HeaderSetResolver(world.CakeContext);
        var headerSet = resolver.ResolveSdl2CoreHeaders(world.RepoRoot.Combine("vcpkg_installed"), "x64-linux-hybrid");
        var linux = PlatformCatalog.CreateSdl2Catalog().ParseViews.Single(view => view.Name == "Linux");
        var runner = new CppAstParseRunner(new ParseDiagnosticFormatter());

        var options = runner.CreateOptions(headerSet, linux);

        await Assert.That(options.TargetSystem).IsEmpty();
        await Assert.That(options.ParseMacros).IsTrue();
        await Assert.That(options.SystemIncludeFolders).Contains(headerSet.IncludeRoot.FullPath);
        await Assert.That(options.Defines).Contains("SDL_DECLSPEC=");
        await Assert.That(options.Defines).Contains("SDL_VIDEO_DRIVER_X11=1");
        await Assert.That(options.AdditionalArguments).Contains("-U_WIN32");
    }
}
