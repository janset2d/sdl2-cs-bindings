using Build.Integrations.DependencyAnalysis;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;

namespace Build.Tests.Unit.Integrations.DependencyAnalysis;

public sealed class LinuxLddScannerTests
{
    [Test]
    public async Task ScanAsync_Should_Return_Only_Existing_Files_From_Ldd_Output()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);

        var binary = new FilePath("/app/bin/libSDL2_image.so");
        fileSystem.CreateFile(binary);

        var existingDependency = new FilePath("/deps/libSDL2-2.0.so.0");
        fileSystem.CreateFile(existingDependency);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithToolPath(new FilePath("/usr/bin/ldd"))
            .WithStandardOutput(
            [
                "libSDL2-2.0.so.0 => /deps/libSDL2-2.0.so.0 (0x00007f)",
                "libmissing.so.1 => /deps/libmissing.so.1 (0x00007f)",
                "libnotfound.so => not found",
            ])
            .Build();

        var scanner = new LinuxLddScanner(context);

        var result = await scanner.ScanAsync(binary);

        await Assert.That(result).Contains(existingDependency);
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ScanAsync_Should_Return_Empty_Set_When_Ldd_Command_Throws()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);

        var binary = new FilePath("/app/bin/libSDL2_image.so");
        fileSystem.CreateFile(binary);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithToolPath(new FilePath("/usr/bin/ldd"))
            .WithStandardOutput([])
            .WithStartException(new CakeException("ldd failed"))
            .Build();

        var scanner = new LinuxLddScanner(context);
        var result = await scanner.ScanAsync(binary);

        await Assert.That(result.Count).IsEqualTo(0);
    }
}
