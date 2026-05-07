using Build.Tests.Fixtures;
using Cake.Core.IO;

namespace Build.Tests.Unit.Fixtures;

public sealed class FakeCakeWorldV2Tests
{
    // ── existing V2 world tests ──

    [Test]
    public async Task Create_Should_Set_Up_Windows_Fake_Environment_By_Default()
    {
        var world = FakeCakeWorldV2.Create();

        await Assert.That(world.Environment.Platform.Family)
            .IsEqualTo(Cake.Core.PlatformFamily.Windows);
        await Assert.That(world.FileSystem).IsNotNull();
        await Assert.That(world.Log).IsNotNull();
        await Assert.That(world.CakeContext).IsNotNull();
    }

    [Test]
    public async Task Create_Should_Set_Up_Unix_Fake_Environment_When_Specified()
    {
        var world = FakeCakeWorldV2.Create(FakeRepoPlatformV2.Unix);

        await Assert.That(world.Environment.Platform.Family)
            .IsEqualTo(Cake.Core.PlatformFamily.Linux);
    }

    [Test]
    public async Task WithTextFile_Should_Create_File_In_Fake_Filesystem()
    {
        var world = FakeCakeWorldV2.Create();

        world.WithTextFile("test/data.json", "{\"key\":42}");

        var fullPath = world.RepoRoot.CombineWithFilePath("test/data.json");
        var exists = world.FileSystem.GetFile(fullPath).Exists;
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task ReadAllText_Should_Return_File_Content()
    {
        var world = FakeCakeWorldV2.Create();
        world.WithTextFile("readme.md", "hello");

        var content = world.ReadAllText("readme.md");

        await Assert.That(content).IsEqualTo("hello");
    }

    [Test]
    public async Task FileExists_Should_Return_True_For_Existing_File()
    {
        var world = FakeCakeWorldV2.Create();
        world.WithTextFile("exists.txt", "");

        await Assert.That(world.FileExists("exists.txt")).IsTrue();
    }

    [Test]
    public async Task FileExists_Should_Return_False_For_Missing_File()
    {
        var world = FakeCakeWorldV2.Create();

        await Assert.That(world.FileExists("missing.txt")).IsFalse();
    }

    // ── process fail-fast tests ──

    [Test]
    public async Task Process_Should_Propagate_Configured_Exit_Code()
    {
        var world = FakeCakeWorldV2.Create()
            .WithProcessResult("myapp", exitCode: 42, stdOut: "");

        var process = world.CakeContext.ProcessRunner.Start(
            new FilePath("myapp"),
            new ProcessSettings());

        process.WaitForExit();
        await Assert.That(process.GetExitCode()).IsEqualTo(42);
    }

    [Test]
    public async Task Process_Should_Capture_Stdout_Lines()
    {
        var world = FakeCakeWorldV2.Create()
            .WithProcessResult("myapp", exitCode: 0, stdOut: "line1\nline2\n");

        var process = world.CakeContext.ProcessRunner.Start(
            new FilePath("myapp"),
            new ProcessSettings());

        var output = process.GetStandardOutput().ToList();
        await Assert.That(output.Count).IsEqualTo(2);
        await Assert.That(output[0]).IsEqualTo("line1");
        await Assert.That(output[1]).IsEqualTo("line2");
    }

    [Test]
    public async Task Process_Should_Capture_Stderr_Lines()
    {
        var world = FakeCakeWorldV2.Create()
            .WithProcessResult("myapp", exitCode: 1, stdOut: "", stdErr: "err1\nerr2\n");

        var process = world.CakeContext.ProcessRunner.Start(
            new FilePath("myapp"),
            new ProcessSettings());

        var errors = process.GetStandardError().ToList();
        await Assert.That(errors.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Process_Command_Matching_Should_Be_Case_Insensitive()
    {
        var world = FakeCakeWorldV2.Create()
            .WithProcessResult("MyApp", exitCode: 7, stdOut: "");

        var process = world.CakeContext.ProcessRunner.Start(
            new FilePath("myapp"),
            new ProcessSettings());

        process.WaitForExit();
        await Assert.That(process.GetExitCode()).IsEqualTo(7);
    }

    [Test]
    public async Task Process_Should_Throw_When_Command_Not_Configured()
    {
        var world = FakeCakeWorldV2.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            world.CakeContext.ProcessRunner.Start(
                new FilePath("unknown"),
                new ProcessSettings());
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task Process_Should_Use_Default_Result_When_Configured()
    {
        var world = FakeCakeWorldV2.Create()
            .WithDefaultProcessResult(exitCode: 99, stdOut: "default");

        var process = world.CakeContext.ProcessRunner.Start(
            new FilePath("anything-unconfigured"),
            new ProcessSettings());

        process.WaitForExit();
        await Assert.That(process.GetExitCode()).IsEqualTo(99);
        var output = process.GetStandardOutput().ToList();
        await Assert.That(output.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(output[0]).IsEqualTo("default");
    }

    // ── process invocation capture tests ──

    [Test]
    public async Task ProcessInvocations_Should_Capture_Command_And_Arguments()
    {
        var world = FakeCakeWorldV2.Create()
            .WithProcessResult("git", exitCode: 0, stdOut: "");

        world.CakeContext.ProcessRunner.Start(
            new FilePath("git"),
            new ProcessSettings { Arguments = "status --short" });

        await Assert.That(world.ProcessInvocations).HasSingleItem();
        var inv = world.ProcessInvocations[0];
        await Assert.That(inv.Command.FullPath).Contains("git");
        await Assert.That(inv.Arguments).IsEqualTo("status --short");
    }

    [Test]
    public async Task ProcessInvocations_Should_Capture_Settings_Flags()
    {
        var world = FakeCakeWorldV2.Create()
            .WithProcessResult("cmd", exitCode: 0, stdOut: "");

        world.CakeContext.ProcessRunner.Start(
            new FilePath("cmd"),
            new ProcessSettings { RedirectStandardOutput = true, Silent = true });

        var inv = world.ProcessInvocations[0];
        await Assert.That(inv.RedirectStandardOutput).IsTrue();
        await Assert.That(inv.Silent).IsTrue();
    }

    // ── tool fail-fast tests ──

    [Test]
    public async Task Tool_Should_Resolve_Configured_Path()
    {
        var world = FakeCakeWorldV2.Create().WithToolPath(new FilePath("/tools/custom"));

        var result = world.CakeContext.Tools.Resolve("any-tool");

        await Assert.That(result.FullPath).IsEqualTo("/tools/custom");
    }

    [Test]
    public async Task Tool_Should_Throw_When_Path_Not_Configured()
    {
        var world = FakeCakeWorldV2.Create();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Task.FromResult(world.CakeContext.Tools.Resolve("any-tool")));

        await Assert.That(exception!.Message).Contains("Tool path was not configured");
    }

    [Test]
    public async Task Tool_Should_Use_Default_Path_When_Configured()
    {
        var world = FakeCakeWorldV2.Create()
            .WithDefaultToolPath(new FilePath("/dev/default"));

        var result = world.CakeContext.Tools.Resolve("anything");

        await Assert.That(result.FullPath).IsEqualTo("/dev/default");
    }
}
