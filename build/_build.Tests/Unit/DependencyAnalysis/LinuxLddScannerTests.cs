using Build.Targets.Harvest.Services;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Tests.Unit.DependencyAnalysis;

public sealed class LinuxLddScannerTests
{
    [Test]
    public async Task ScanAsync_Should_Return_Only_Existing_Files_From_Ldd_Output()
    {
        var binary = new FilePath("/app/bin/libSDL2_image.so");
        var existingDependency = new FilePath("/deps/libSDL2-2.0.so.0");

        var world = FakeCakeWorld.CreateLinux()
            .WithBinaryFile(binary, [])
            .WithBinaryFile(existingDependency, [])
            .WithToolPath(new FilePath("/usr/bin/ldd"))
            .WithProcessResult("ldd", exitCode: 0, stdOut: string.Join('\n',
                "libSDL2-2.0.so.0 => /deps/libSDL2-2.0.so.0 (0x00007f)",
                "libmissing.so.1 => /deps/libmissing.so.1 (0x00007f)",
                "libnotfound.so => not found"));

        var scanner = new LinuxLddScanner(world.CakeContext);

        var result = await scanner.ScanAsync(binary);

        await Assert.That(result).Contains(existingDependency);
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ScanAsync_Should_Return_Empty_Set_When_Ldd_Command_Throws()
    {
        var binary = new FilePath("/app/bin/libSDL2_image.so");

        var world = FakeCakeWorld.CreateLinux()
            .WithBinaryFile(binary, [])
            .WithToolPath(new FilePath("/usr/bin/ldd"))
            .WithProcessException("ldd", new CakeException("ldd failed"));

        var scanner = new LinuxLddScanner(world.CakeContext);
        var result = await scanner.ScanAsync(binary);

        await Assert.That(result.Count).IsEqualTo(0);
    }
}
