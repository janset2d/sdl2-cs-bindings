using Build.Tools.Dumpbin;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using Cake.Testing;

namespace Build.Tests.Unit.Tools.Dumpbin;

public sealed class DumpbinDependentsToolTests
{
    [Test]
    public async Task RunDependents_Should_Return_Output_And_Pass_Dependents_Arguments()
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var toolPath = new FilePath("C:/tools/dumpbin.exe");
        fileSystem.CreateFile(toolPath);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithProcessCapture(out var capture)
            .WithToolPath(toolPath)
            .WithStandardOutput(["SDL2.dll", "zlib1.dll"])
            .Build();
        var tool = new DumpbinDependentsTool(context);

        var settings = new DumpbinDependentsSettings("C:/app/bin/SDL2_image.dll")
        {
            ToolPath = toolPath,
        };

        var output = tool.RunDependents(settings);

        await Assert.That(output).IsEqualTo($"SDL2.dll{Environment.NewLine}zlib1.dll");
        await Assert.That(capture.Settings).IsNotNull();

        var renderedArgs = capture.Settings!.Arguments.Render();
        await Assert.That(renderedArgs).Contains("/dependents");
        await Assert.That(renderedArgs).Contains("SDL2_image.dll");
        await Assert.That(capture.Settings.RedirectStandardOutput).IsTrue();
        await Assert.That(capture.Settings.RedirectStandardError).IsTrue();
    }

    [Test]
    public async Task RunDependents_Should_Return_Null_When_Process_Output_Is_Empty()
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var toolPath = new FilePath("C:/tools/dumpbin.exe");
        fileSystem.CreateFile(toolPath);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithProcessCapture(out _)
            .WithToolPath(toolPath)
            .WithStandardOutput([])
            .Build();
        var tool = new DumpbinDependentsTool(context);

        var settings = new DumpbinDependentsSettings("C:/app/bin/SDL2_image.dll")
        {
            ToolPath = toolPath,
        };

        var output = tool.RunDependents(settings);

        await Assert.That(output).IsNull();
    }

    [Test]
    public async Task RunDependents_Should_Throw_When_DependentsPath_Is_Empty()
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var toolPath = new FilePath("C:/tools/dumpbin.exe");
        fileSystem.CreateFile(toolPath);

        var context = new FakeCakeToolContextBuilder(fileSystem, environment)
            .WithProcessCapture(out _)
            .WithToolPath(toolPath)
            .WithStandardOutput([])
            .Build();
        var tool = new DumpbinDependentsTool(context);

        var settings = new DumpbinDependentsSettings(string.Empty)
        {
            ToolPath = toolPath,
        };

        await Assert.That(() => tool.RunDependents(settings)).Throws<InvalidOperationException>();
    }
}
