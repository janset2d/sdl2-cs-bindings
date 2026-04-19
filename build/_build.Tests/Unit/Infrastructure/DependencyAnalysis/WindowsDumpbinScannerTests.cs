using Build.Infrastructure.DependencyAnalysis;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using Cake.Testing;

namespace Build.Tests.Unit.Infrastructure.DependencyAnalysis;

public sealed class WindowsDumpbinScannerTests
{
    [Test]
    public async Task ScanAsync_Should_Return_Only_Existing_Dependencies_From_Dumpbin_Output()
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FakeFileSystem(environment);

        var binary = new FilePath("C:/app/bin/SDL2_image.dll");
        fileSystem.CreateFile(binary);

        var existingDependency = new FilePath("C:/app/bin/SDL2.dll");
        fileSystem.CreateFile(existingDependency);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithToolPath(new FilePath("C:/tools/dumpbin.exe"))
            .WithStandardOutput(
            [
                "Dump of file SDL2_image.dll",
                "Image has the following dependencies:",
                "    SDL2.dll",
                "    zlib1.dll",
                "Summary",
            ])
            .Build();

        var scanner = new WindowsDumpbinScanner(context);

        var result = await scanner.ScanAsync(binary);

        await Assert.That(result).Contains(existingDependency);
        await Assert.That(result.Select(path => path.FullPath)).DoesNotContain("C:/app/bin/zlib1.dll");
    }

    [Test]
    public async Task ScanAsync_Should_Return_Empty_Set_When_Dumpbin_Produces_No_Output()
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var binary = new FilePath("C:/app/bin/SDL2_image.dll");
        fileSystem.CreateFile(binary);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithToolPath(new FilePath("C:/tools/dumpbin.exe"))
            .WithStandardOutput([])
            .Build();

        var scanner = new WindowsDumpbinScanner(context);
        var result = await scanner.ScanAsync(binary);

        await Assert.That(result.Count).IsEqualTo(0);
    }
}
