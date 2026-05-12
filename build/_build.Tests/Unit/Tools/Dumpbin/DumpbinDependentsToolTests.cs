using Build.Tests.Fixtures;
using Build.Tools.Dumpbin;
using Cake.Core.IO;

namespace Build.Tests.Unit.Tools.Dumpbin;

public sealed class DumpbinDependentsToolTests
{
    [Test]
    public async Task RunDependents_Should_Return_Output_And_Pass_Dependents_Arguments()
    {
        var toolPath = new FilePath("C:/tools/dumpbin.exe");

        var world = FakeCakeWorld.CreateWindows()
            .WithToolPath(toolPath)
            .WithProcessResult("dumpbin.exe", exitCode: 0, stdOut: string.Join('\n', "SDL2.dll", "zlib1.dll"));
        var tool = new DumpbinDependentsTool(world.CakeContext);

        var settings = new DumpbinDependentsSettings("C:/app/bin/SDL2_image.dll")
        {
            ToolPath = toolPath,
        };

        var output = tool.RunDependents(settings);

        await Assert.That(output).IsEqualTo($"SDL2.dll{Environment.NewLine}zlib1.dll");

        var invocation = world.ProcessInvocations.Single();
        var renderedArgs = invocation.Arguments;
        await Assert.That(renderedArgs).Contains("/dependents");
        await Assert.That(renderedArgs).Contains("SDL2_image.dll");
        await Assert.That(invocation.RedirectStandardOutput).IsTrue();
        await Assert.That(invocation.RedirectStandardError).IsTrue();
    }

    [Test]
    public async Task RunDependents_Should_Return_Null_When_Process_Output_Is_Empty()
    {
        var toolPath = new FilePath("C:/tools/dumpbin.exe");

        var world = FakeCakeWorld.CreateWindows()
            .WithToolPath(toolPath)
            .WithProcessResult("dumpbin.exe", exitCode: 0, stdOut: "");
        var tool = new DumpbinDependentsTool(world.CakeContext);

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
        var toolPath = new FilePath("C:/tools/dumpbin.exe");

        var world = FakeCakeWorld.CreateWindows()
            .WithToolPath(toolPath)
            .WithProcessResult("dumpbin.exe", exitCode: 0, stdOut: "");
        var tool = new DumpbinDependentsTool(world.CakeContext);

        var settings = new DumpbinDependentsSettings(string.Empty)
        {
            ToolPath = toolPath,
        };

        await Assert.That(() => tool.RunDependents(settings)).Throws<InvalidOperationException>();
    }
}
