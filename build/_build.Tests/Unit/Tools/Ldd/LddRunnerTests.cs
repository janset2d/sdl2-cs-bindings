using Build.Tests.Fixtures;
using Build.Tools.Ldd;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;
using NSubstitute;

namespace Build.Tests.Unit.Tools.Ldd;

public sealed class LddRunnerTests
{
    [Test]
    public async Task GetDependencies_Should_Throw_When_Platform_Is_Not_Unix()
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var processRunner = Substitute.For<IProcessRunner>();
        var toolLocator = Substitute.For<IToolLocator>();

        var runner = new LddRunner(fileSystem, environment, processRunner, toolLocator);
        var settings = new LddSettings(new FilePath("C:/app/bin/SDL2.dll"));

        await Assert.That(() => runner.GetDependencies(settings)).Throws<PlatformNotSupportedException>();
    }

    [Test]
    public async Task GetDependencies_Should_Pass_Flag_Arguments_And_File_Path()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var lddPath = new FilePath("/usr/bin/ldd");
        fileSystem.CreateFile(lddPath);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithProcessCapture(out var capture)
            .WithToolPath(lddPath)
            .WithStandardOutput(["libSDL2-2.0.so.0 => /deps/libSDL2-2.0.so.0 (0x00007f)"])
            .Build();

        var runner = new LddRunner(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools);
        var settings = new LddSettings(new FilePath("/app/bin/libSDL2_image.so"))
        {
            ShowUnused = true,
            PerformRelocations = true,
            IncludeData = true,
            Verbose = true,
        };

        var output = runner.GetDependencies(settings);

        await Assert.That(output).Contains("libSDL2-2.0.so.0");
        await Assert.That(capture.Settings).IsNotNull();

        var renderedArgs = capture.Settings!.Arguments.Render();
        await Assert.That(renderedArgs).Contains("-u");
        await Assert.That(renderedArgs).Contains("-r");
        await Assert.That(renderedArgs).Contains("-d");
        await Assert.That(renderedArgs).Contains("-v");
        await Assert.That(renderedArgs).Contains("/app/bin/libSDL2_image.so");
    }

    [Test]
    public async Task GetDependenciesAsDictionary_Should_Parse_Redirected_And_Direct_Libraries()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var lddPath = new FilePath("/usr/bin/ldd");
        fileSystem.CreateFile(lddPath);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithProcessCapture(out _)
            .WithToolPath(lddPath)
            .WithStandardOutput(
            [
                "libSDL2-2.0.so.0 => /deps/libSDL2-2.0.so.0 (0x00007f)",
                "libmissing.so.1 => not found",
                "/lib64/ld-linux-x86-64.so.2 (0x00007f)",
            ])
            .Build();

        var runner = new LddRunner(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools);
        var settings = new LddSettings(new FilePath("/app/bin/libSDL2_image.so"));

        var dependencies = runner.GetDependenciesAsDictionary(settings);

        await Assert.That(dependencies).ContainsKey("libSDL2-2.0.so.0");
        await Assert.That(dependencies["libSDL2-2.0.so.0"]).IsEqualTo("/deps/libSDL2-2.0.so.0");
        await Assert.That(dependencies).ContainsKey("ld-linux-x86-64.so.2");
        await Assert.That(dependencies).DoesNotContainKey("libmissing.so.1");
    }
}
