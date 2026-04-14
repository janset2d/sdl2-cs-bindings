using Build.Modules.DependencyAnalysis;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;

namespace Build.Tests.Unit.Modules.DependencyAnalysis;

public sealed class MacOtoolScannerTests
{
    [Test]
    public async Task ScanAsync_Should_Resolve_Rpath_And_LoaderPath_Dependencies()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);

        var binary = new FilePath("/app/bin/libSDL2_image.dylib");
        fileSystem.CreateFile(binary);

        var resolvedRpathDependency = new FilePath("/app/bin/libSDL2.dylib");
        var resolvedLoaderPathDependency = new FilePath("/app/bin/plugins/libcodec.dylib");
        fileSystem.CreateFile(resolvedRpathDependency);
        fileSystem.CreateFile(resolvedLoaderPathDependency);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithToolPath(new FilePath("/usr/bin/otool"))
            .WithStandardOutput(
            [
                "/app/bin/libSDL2_image.dylib:",
                "    @rpath/libSDL2.dylib (compatibility version 1.0.0, current version 1.0.0)",
                "    @loader_path/plugins/libcodec.dylib (compatibility version 1.0.0, current version 1.0.0)",
                "    /System/Library/Frameworks/CoreFoundation.framework/Versions/A/CoreFoundation (compatibility version 150.0.0, current version 1856.105.0)",
            ])
            .Build();

        var scanner = new MacOtoolScanner(context);

        var result = await scanner.ScanAsync(binary);

        await Assert.That(result).Contains(resolvedRpathDependency);
        await Assert.That(result).Contains(resolvedLoaderPathDependency);
        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ScanAsync_Should_Return_Empty_Set_When_Otool_Command_Throws()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);

        var binary = new FilePath("/app/bin/libSDL2_image.dylib");
        fileSystem.CreateFile(binary);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithToolPath(new FilePath("/usr/bin/otool"))
            .WithStandardOutput([])
            .WithStartException(new CakeException("otool failed"))
            .Build();

        var scanner = new MacOtoolScanner(context);
        var result = await scanner.ScanAsync(binary);

        await Assert.That(result.Count).IsEqualTo(0);
    }
}
