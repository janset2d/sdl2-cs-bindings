using Build.Tests.Fixtures;
using Build.Tools.Ldd;
using Cake.Core.IO;

namespace Build.Tests.Unit.Tools.Ldd;

public sealed class LddRunnerTests
{
    [Test]
    public async Task GetDependencies_Should_Throw_When_Platform_Is_Not_Unix()
    {
        var world = FakeCakeWorld.CreateWindows();

        var runner = new LddRunner(world.FileSystem, world.Environment, world.CakeContext.ProcessRunner, world.CakeContext.Tools);
        var settings = new LddSettings(new FilePath("C:/app/bin/SDL2.dll"));

        await Assert.That(() => runner.GetDependencies(settings)).Throws<PlatformNotSupportedException>();
    }

    [Test]
    public async Task GetDependencies_Should_Pass_Flag_Arguments_And_File_Path()
    {
        var lddPath = new FilePath("/usr/bin/ldd");

        var world = FakeCakeWorld.CreateLinux()
            .WithToolPath(lddPath)
            .WithProcessResult("ldd", exitCode: 0, stdOut: "libSDL2-2.0.so.0 => /deps/libSDL2-2.0.so.0 (0x00007f)");

        var runner = new LddRunner(world.FileSystem, world.Environment, world.CakeContext.ProcessRunner, world.CakeContext.Tools);
        var settings = new LddSettings(new FilePath("/app/bin/libSDL2_image.so"))
        {
            ShowUnused = true,
            PerformRelocations = true,
            IncludeData = true,
            Verbose = true,
        };

        var output = runner.GetDependencies(settings);

        await Assert.That(output).Contains("libSDL2-2.0.so.0");

        var renderedArgs = world.ProcessInvocations.Single().Arguments;
        await Assert.That(renderedArgs).Contains("-u");
        await Assert.That(renderedArgs).Contains("-r");
        await Assert.That(renderedArgs).Contains("-d");
        await Assert.That(renderedArgs).Contains("-v");
        await Assert.That(renderedArgs).Contains("/app/bin/libSDL2_image.so");
    }

    [Test]
    public async Task GetDependenciesAsDictionary_Should_Parse_Redirected_And_Direct_Libraries()
    {
        var lddPath = new FilePath("/usr/bin/ldd");

        var world = FakeCakeWorld.CreateLinux()
            .WithToolPath(lddPath)
            .WithProcessResult("ldd", exitCode: 0, stdOut: string.Join('\n',
                "libSDL2-2.0.so.0 => /deps/libSDL2-2.0.so.0 (0x00007f)",
                "libmissing.so.1 => not found",
                "/lib64/ld-linux-x86-64.so.2 (0x00007f)"));

        var runner = new LddRunner(world.FileSystem, world.Environment, world.CakeContext.ProcessRunner, world.CakeContext.Tools);
        var settings = new LddSettings(new FilePath("/app/bin/libSDL2_image.so"));

        var dependencies = runner.GetDependenciesAsDictionary(settings);

        await Assert.That(dependencies).ContainsKey("libSDL2-2.0.so.0");
        await Assert.That(dependencies["libSDL2-2.0.so.0"]).IsEqualTo("/deps/libSDL2-2.0.so.0");
        await Assert.That(dependencies).ContainsKey("ld-linux-x86-64.so.2");
        await Assert.That(dependencies).DoesNotContainKey("libmissing.so.1");
    }
}
