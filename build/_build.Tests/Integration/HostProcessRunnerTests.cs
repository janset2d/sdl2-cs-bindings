using Build.Host.Processes;
using Build.Targets.NativeSmoke.Services;
using Build.Tests.Fixtures;
using NSubstitute;

namespace Build.Tests.Integration;

public sealed class HostProcessRunnerTests
{
    [Test]
    public async Task RunAsync_Should_Return_DotNet_Version_As_Utf8()
    {
        var result = await new HostProcessRunner().RunAsync(new HostProcessRequest("dotnet", ["--version"]), CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StandardOutput.Trim()).IsNotEmpty();
        await Assert.That(result.StandardError).IsEmpty();
    }

    [Test]
    public async Task RunAsync_Should_Decode_NonAscii_Utf8_From_Both_Streams()
    {
        using var directory = new TempDirectory("janset-utf8-child-");
        var source = Path.Combine(directory.Path, "child.cs");
        await File.WriteAllTextAsync(source, HostEnvironmentData.Utf8ChildSource);

        var result = await new HostProcessRunner().RunAsync(new HostProcessRequest("dotnet", ["run", "--file", source]), CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StandardOutput).IsEqualTo(HostEnvironmentData.Utf8Payload);
        await Assert.That(result.StandardError).IsEqualTo(HostEnvironmentData.Utf8Payload);
    }

    [Test]
    [WindowsOnly]
    public async Task RunAsync_Should_Capture_A_Batch_Under_A_Path_Without_Spaces()
    {
        using var directory = new TempDirectory("janset-batch-");
        await Assert.That(directory.Path).DoesNotContain(" ");
        var batch = Path.Combine(directory.Path, "VC", "Auxiliary", "Build", "vcvarsall.bat");
        Directory.CreateDirectory(Path.GetDirectoryName(batch)!);
        await File.WriteAllTextAsync(batch, HostEnvironmentData.BatchProbeSource);
        var world = HostEnvironmentData.MsvcWorld(installation: directory.Path.Replace('\\', '/'));
        world.HostProcessRunner
            .RunAsync(Arg.Any<HostProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new HostProcessRunner().RunAsync(call.Arg<HostProcessRequest>(), call.Arg<CancellationToken>()));
        var resolver = new MsvcDevEnvironment(world.CakeContext, world.Log, world.HostProcessRunner);

        var result = await resolver.ResolveAsync(MsvcTargetArch.X64);

        await Assert.That(result["JANSET_BATCH_PROBE"]).IsEqualTo("captured");
    }
}
