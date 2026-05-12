using Build.Targets.Harvest.Services;
using Build.Tests.Fixtures;
using Cake.Core.IO;

namespace Build.Tests.Unit.DependencyAnalysis;

public sealed class WindowsDumpbinScannerTests
{
    [Test]
    public async Task ScanAsync_Should_Return_Only_Existing_Dependencies_From_Dumpbin_Output()
    {
        var binary = new FilePath("C:/app/bin/SDL2_image.dll");
        var existingDependency = new FilePath("C:/app/bin/SDL2.dll");

        var world = FakeCakeWorld.CreateWindows()
            .WithBinaryFile(binary, [])
            .WithBinaryFile(existingDependency, [])
            .WithToolPath(new FilePath("C:/tools/dumpbin.exe"))
            .WithProcessResult("dumpbin.exe", exitCode: 0, stdOut: string.Join('\n',
                "Dump of file SDL2_image.dll",
                "Image has the following dependencies:",
                "    SDL2.dll",
                "    zlib1.dll",
                "Summary"));

        var scanner = new WindowsDumpbinScanner(world.CakeContext);

        var result = await scanner.ScanAsync(binary);

        await Assert.That(result).Contains(existingDependency);
        await Assert.That(result.Select(path => path.FullPath)).DoesNotContain("C:/app/bin/zlib1.dll");
    }

    [Test]
    public async Task ScanAsync_Should_Return_Empty_Set_When_Dumpbin_Produces_No_Output()
    {
        var binary = new FilePath("C:/app/bin/SDL2_image.dll");

        var world = FakeCakeWorld.CreateWindows()
            .WithBinaryFile(binary, [])
            .WithToolPath(new FilePath("C:/tools/dumpbin.exe"))
            .WithProcessResult("dumpbin.exe", exitCode: 0, stdOut: "");

        var scanner = new WindowsDumpbinScanner(world.CakeContext);
        var result = await scanner.ScanAsync(binary);

        await Assert.That(result.Count).IsEqualTo(0);
    }
}
