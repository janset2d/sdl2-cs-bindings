using Build.Targets.Harvest.Services;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Tests.Unit.DependencyAnalysis;

public sealed class MacOtoolScannerTests
{
    [Test]
    public async Task ScanAsync_Should_Resolve_Rpath_And_LoaderPath_Dependencies()
    {
        var binary = new FilePath("/app/bin/libSDL2_image.dylib");
        var resolvedRpathDependency = new FilePath("/app/bin/libSDL2.dylib");
        var resolvedLoaderPathDependency = new FilePath("/app/bin/plugins/libcodec.dylib");

        var world = FakeCakeWorld.CreateOsx()
            .WithBinaryFile(binary, [])
            .WithBinaryFile(resolvedRpathDependency, [])
            .WithBinaryFile(resolvedLoaderPathDependency, [])
            .WithToolPath(new FilePath("/usr/bin/otool"))
            .WithProcessResult("otool", exitCode: 0, stdOut: string.Join('\n',
                "/app/bin/libSDL2_image.dylib:",
                "    @rpath/libSDL2.dylib (compatibility version 1.0.0, current version 1.0.0)",
                "    @loader_path/plugins/libcodec.dylib (compatibility version 1.0.0, current version 1.0.0)",
                "    /System/Library/Frameworks/CoreFoundation.framework/Versions/A/CoreFoundation (compatibility version 150.0.0, current version 1856.105.0)"));

        var scanner = new MacOtoolScanner(world.CakeContext);

        var result = await scanner.ScanAsync(binary);

        await Assert.That(result).Contains(resolvedRpathDependency);
        await Assert.That(result).Contains(resolvedLoaderPathDependency);
        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ScanAsync_Should_Return_Empty_Set_When_Otool_Command_Throws()
    {
        var binary = new FilePath("/app/bin/libSDL2_image.dylib");

        var world = FakeCakeWorld.CreateOsx()
            .WithBinaryFile(binary, [])
            .WithToolPath(new FilePath("/usr/bin/otool"))
            .WithProcessException("otool", new CakeException("otool failed"));

        var scanner = new MacOtoolScanner(world.CakeContext);
        var result = await scanner.ScanAsync(binary);

        await Assert.That(result.Count).IsEqualTo(0);
    }
}
