using Build.Tests.Fixtures;
using Build.Tools.Otool;
using Cake.Core.IO;

namespace Build.Tests.Unit.Tools.Otool;

public sealed class OtoolRunnerTests
{
    [Test]
    public async Task GetOutput_Should_Pass_Configured_Arguments_To_Otool()
    {
        var otoolPath = new FilePath("/usr/bin/otool");

        var world = FakeCakeWorld.CreateOsx()
            .WithToolPath(otoolPath)
            .WithProcessResult("otool", exitCode: 0, stdOut: "/app/bin/libSDL2_image.dylib:");

        var runner = new OtoolRunner(world.CakeContext);
        var settings = new OtoolSettings(new FilePath("/app/bin/libSDL2_image.dylib"))
        {
            ShowLibraries = true,
            ShowLoadCommands = true,
            ShowHeader = true,
            Verbose = true,
        };

        _ = runner.GetOutput(settings);

        var renderedArgs = world.ProcessInvocations.Single().Arguments;

        await Assert.That(renderedArgs).Contains("-L");
        await Assert.That(renderedArgs).Contains("-l");
        await Assert.That(renderedArgs).Contains("-h");
        await Assert.That(renderedArgs).Contains("-v");
        await Assert.That(renderedArgs).Contains("/app/bin/libSDL2_image.dylib");
    }

    [Test]
    public async Task GetDependenciesAsDictionary_Should_Parse_Dylib_And_Framework_Names()
    {
        var otoolPath = new FilePath("/usr/bin/otool");

        var world = FakeCakeWorld.CreateOsx()
            .WithToolPath(otoolPath)
            .WithProcessResult("otool", exitCode: 0, stdOut: string.Join('\n',
                "/app/bin/libSDL2_image.dylib:",
                "\t/usr/lib/libSystem.B.dylib (compatibility version 1.0.0, current version 1351.0.0)",
                "\t/System/Library/Frameworks/CoreFoundation.framework/Versions/A/CoreFoundation (compatibility version 150.0.0, current version 1856.105.0)"));

        var runner = new OtoolRunner(world.CakeContext);
        var settings = new OtoolSettings(new FilePath("/app/bin/libSDL2_image.dylib"));

        var dependencies = runner.GetDependenciesAsDictionary(settings);

        await Assert.That(dependencies).ContainsKey("libSystem.B.dylib");
        await Assert.That(dependencies["libSystem.B.dylib"]).IsEqualTo("/usr/lib/libSystem.B.dylib");
        await Assert.That(dependencies).ContainsKey("CoreFoundation.framework");
        await Assert.That(dependencies["CoreFoundation.framework"]).IsEqualTo("/System/Library/Frameworks/CoreFoundation.framework/Versions/A/CoreFoundation");
    }

}
