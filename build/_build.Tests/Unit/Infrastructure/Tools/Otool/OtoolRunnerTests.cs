using Build.Infrastructure.Tools.Otool;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using Cake.Testing;

namespace Build.Tests.Unit.Infrastructure.Tools.Otool;

public sealed class OtoolRunnerTests
{
    [Test]
    public async Task GetOutput_Should_Pass_Configured_Arguments_To_Otool()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var otoolPath = new FilePath("/usr/bin/otool");
        fileSystem.CreateFile(otoolPath);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithProcessCapture(out var capture)
            .WithToolPath(otoolPath)
            .WithStandardOutput([
                "/app/bin/libSDL2_image.dylib:",
            ])
            .Build();

        var runner = new OtoolRunner(context);
        var settings = new OtoolSettings(new FilePath("/app/bin/libSDL2_image.dylib"))
        {
            ShowLibraries = true,
            ShowLoadCommands = true,
            ShowHeader = true,
            Verbose = true,
        };

        _ = runner.GetOutput(settings);

        await Assert.That(capture.Settings).IsNotNull();
        var renderedArgs = capture.Settings!.Arguments.Render();

        await Assert.That(renderedArgs).Contains("-L");
        await Assert.That(renderedArgs).Contains("-l");
        await Assert.That(renderedArgs).Contains("-h");
        await Assert.That(renderedArgs).Contains("-v");
        await Assert.That(renderedArgs).Contains("/app/bin/libSDL2_image.dylib");
    }

    [Test]
    public async Task GetDependenciesAsDictionary_Should_Parse_Dylib_And_Framework_Names()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var otoolPath = new FilePath("/usr/bin/otool");
        fileSystem.CreateFile(otoolPath);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithProcessCapture(out _)
            .WithToolPath(otoolPath)
            .WithStandardOutput(
            [
                "/app/bin/libSDL2_image.dylib:",
                "\t/usr/lib/libSystem.B.dylib (compatibility version 1.0.0, current version 1351.0.0)",
                "\t/System/Library/Frameworks/CoreFoundation.framework/Versions/A/CoreFoundation (compatibility version 150.0.0, current version 1856.105.0)",
            ])
            .Build();

        var runner = new OtoolRunner(context);
        var settings = new OtoolSettings(new FilePath("/app/bin/libSDL2_image.dylib"));

        var dependencies = runner.GetDependenciesAsDictionary(settings);

        await Assert.That(dependencies).ContainsKey("libSystem.B.dylib");
        await Assert.That(dependencies["libSystem.B.dylib"]).IsEqualTo("/usr/lib/libSystem.B.dylib");
        await Assert.That(dependencies).ContainsKey("CoreFoundation.framework");
        await Assert.That(dependencies["CoreFoundation.framework"]).IsEqualTo("/System/Library/Frameworks/CoreFoundation.framework/Versions/A/CoreFoundation");
    }

}
