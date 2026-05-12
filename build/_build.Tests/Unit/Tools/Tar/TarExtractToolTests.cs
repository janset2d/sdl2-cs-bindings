using Build.Tests.Fixtures;
using Build.Tools.Tar;
using Cake.Core.IO;

namespace Build.Tests.Unit.Tools.Tar;

public sealed class TarExtractToolTests
{
    [Test]
    public async Task Extract_Should_Invoke_Tar_With_Xzf_And_C_Destination()
    {
        var tarPath = new FilePath("/usr/bin/tar");

        var world = FakeCakeWorld.CreateLinux()
            .WithToolPath(tarPath)
            .WithProcessResult("tar", exitCode: 0, stdOut: "");

        var tool = new TarExtractTool(world.FileSystem, world.Environment, world.CakeContext.ProcessRunner, world.CakeContext.Tools);
        var settings = new TarExtractSettings(
            new FilePath("/repo/artifacts/harvest_output/SDL2/runtimes/linux-x64/native/native.tar.gz"),
            new DirectoryPath("/repo/artifacts/temp/inspect/linux-x64/SDL2"));

        tool.Extract(settings);

        var args = world.ProcessInvocations.Single().Arguments;
        await Assert.That(args).StartsWith("-xzf ");
        await Assert.That(args).Contains("native.tar.gz");
        await Assert.That(args).Contains("-C ");
        await Assert.That(args).Contains("/repo/artifacts/temp/inspect/linux-x64/SDL2");
    }

    [Test]
    public async Task Extract_Should_Append_V_When_Verbose_Enabled()
    {
        var tarPath = new FilePath("/usr/bin/tar");

        var world = FakeCakeWorld.CreateLinux()
            .WithToolPath(tarPath)
            .WithProcessResult("tar", exitCode: 0, stdOut: "");

        var tool = new TarExtractTool(world.FileSystem, world.Environment, world.CakeContext.ProcessRunner, world.CakeContext.Tools);
        var settings = new TarExtractSettings(
            new FilePath("/tmp/a.tar.gz"),
            new DirectoryPath("/tmp/out"))
        {
            Verbose = true,
        };

        tool.Extract(settings);

        var args = world.ProcessInvocations.Single().Arguments;
        await Assert.That(args).StartsWith("-xzvf ");
    }

    [Test]
    public async Task Extract_Should_Append_StripComponents_When_Set()
    {
        var tarPath = new FilePath("/usr/bin/tar");

        var world = FakeCakeWorld.CreateLinux()
            .WithToolPath(tarPath)
            .WithProcessResult("tar", exitCode: 0, stdOut: "");

        var tool = new TarExtractTool(world.FileSystem, world.Environment, world.CakeContext.ProcessRunner, world.CakeContext.Tools);
        var settings = new TarExtractSettings(
            new FilePath("/tmp/a.tar.gz"),
            new DirectoryPath("/tmp/out"))
        {
            StripComponents = 2,
        };

        tool.Extract(settings);

        var args = world.ProcessInvocations.Single().Arguments;
        await Assert.That(args).Contains("--strip-components=2");
    }

    [Test]
    public async Task Extract_Should_Throw_When_Settings_Null()
    {
        var tarPath = new FilePath("/usr/bin/tar");

        var world = FakeCakeWorld.CreateLinux()
            .WithToolPath(tarPath)
            .WithProcessResult("tar", exitCode: 0, stdOut: "");

        var tool = new TarExtractTool(world.FileSystem, world.Environment, world.CakeContext.ProcessRunner, world.CakeContext.Tools);

        await Assert.That(() => tool.Extract(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task TarExtractSettings_Should_Throw_When_Archive_Null()
    {
        await Assert.That(() => new TarExtractSettings(null!, new DirectoryPath("/dest")))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task TarExtractSettings_Should_Throw_When_Destination_Null()
    {
        await Assert.That(() => new TarExtractSettings(new FilePath("/a.tar.gz"), null!))
            .Throws<ArgumentNullException>();
    }
}
