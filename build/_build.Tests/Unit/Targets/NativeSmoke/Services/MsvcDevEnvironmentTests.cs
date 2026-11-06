using Build.Targets.NativeSmoke.Services;
using Build.Tests.Fixtures;
using Cake.Core.Diagnostics;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.NativeSmoke.Services;

/// <summary>Exercises environment resolution through the Cake world and host-process seam.</summary>
public sealed class MsvcDevEnvironmentTests
{
    [Test]
    [WindowsOnly]
    public async Task ResolveAsync_Should_Preserve_The_Raw_Batch_Command_Line()
    {
        var world = HostEnvironmentData.MsvcWorld();
        var resolver = new MsvcDevEnvironment(world.CakeContext, world.Log, world.HostProcessRunner);

        await resolver.ResolveAsync(MsvcTargetArch.X64);

        await Assert.That(world.HostProcessInvocations.Single().Request.RawArguments).IsEqualTo(HostEnvironmentData.CaptureCommand);
    }

    [Test]
    [WindowsOnly]
    public async Task ResolveAsync_Should_Read_Cake_Environment_And_Cache_The_Delta()
    {
        var world = HostEnvironmentData.MsvcWorld();
        using var cancellation = new CancellationTokenSource();
        var resolver = new MsvcDevEnvironment(world.CakeContext, world.Log, world.HostProcessRunner);

        var result = await resolver.ResolveAsync(MsvcTargetArch.X64, cancellation.Token);
        var cached = await resolver.ResolveAsync(MsvcTargetArch.X64, cancellation.Token);

        await Assert.That(result.Select(pair => (pair.Key, pair.Value))).IsEquivalentTo([("CHANGED", "after"), ("UNICODE", "İstanbul")], CollectionOrdering.Matching);
        await Assert.That(cached).IsSameReferenceAs(result);
        await Assert.That(world.HostProcessInvocations.Count).IsEqualTo(1);
        var invocation = world.HostProcessInvocations[0];
        await Assert.That(invocation.CancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(new System.IO.FileInfo(invocation.Request.FileName).Name).IsEqualTo("cmd.exe");
        await Assert.That(invocation.Request.RawArguments).IsEqualTo(HostEnvironmentData.CaptureCommand);
    }

    [Test]
    [WindowsOnly]
    public async Task ResolveAsync_Should_Use_The_Cake_Parent_Marker_Without_Starting_A_Process()
    {
        var world = FakeCakeWorld.CreateWindows();
        world.Log.Verbosity = Verbosity.Diagnostic;
        world.Environment.SetEnvironmentVariable("VCToolsInstallDir", HostEnvironmentData.ParentMarker);
        var resolver = new MsvcDevEnvironment(world.CakeContext, world.Log, world.HostProcessRunner);

        var result = await resolver.ResolveAsync(MsvcTargetArch.X64);

        await Assert.That(result).IsEmpty();
        await Assert.That(world.ProcessInvocations).IsEmpty();
        await Assert.That(world.HostProcessInvocations).IsEmpty();
        await Assert.That(world.Log.HasMessage(LogLevel.Verbose, HostEnvironmentData.ParentMarker)).IsTrue();
    }

    [Test]
    [WindowsOnly]
    public async Task ResolveAsync_Should_Throw_When_The_Batch_File_Is_Missing()
    {
        var world = HostEnvironmentData.MsvcWorld();
        world.FileSystem.GetFile(HostEnvironmentData.Batch).Delete();
        var resolver = new MsvcDevEnvironment(world.CakeContext, world.Log, world.HostProcessRunner);

        var error = await Assert.That(async () => await resolver.ResolveAsync(MsvcTargetArch.X64)).Throws<Cake.Core.CakeException>();

        await Assert.That(error!.Message).IsEqualTo(
            "MsvcDevEnvironment: vcvarsall.bat not found at 'C:/Visual Studio/VC/Auxiliary/Build/vcvarsall.bat' under Visual Studio installation 'C:/Visual Studio'. " +
            "Reinstall the VC Tools workload or run Cake from a Developer PowerShell.");
    }

    [Test]
    [WindowsOnly]
    public async Task ResolveAsync_Should_Report_The_Process_Exit_And_Error()
    {
        var world = HostEnvironmentData.MsvcWorld(17, "capture failed");
        var resolver = new MsvcDevEnvironment(world.CakeContext, world.Log, world.HostProcessRunner);

        var error = await Assert.That(async () => await resolver.ResolveAsync(MsvcTargetArch.X64)).Throws<Cake.Core.CakeException>();

        await Assert.That(error!.Message).IsEqualTo("MsvcDevEnvironment: 'cmd /c vcvarsall.bat x64' exited with code 17. stderr: capture failed");
    }

    [Test]
    [NonWindowsOnly]
    public async Task ResolveAsync_Should_Throw_PlatformNotSupportedException_When_Host_Is_Not_Windows()
    {
        var world = FakeCakeWorld.CreateLinux();
        var resolver = new MsvcDevEnvironment(world.CakeContext, Substitute.For<ICakeLog>(), world.HostProcessRunner);

        var thrown = await Assert.That(async () => await resolver.ResolveAsync(MsvcTargetArch.X64)).Throws<PlatformNotSupportedException>();
        await Assert.That(thrown!.Message).Contains("Windows-only");
        await Assert.That(thrown.Message).Contains("OperatingSystem.IsWindows");
    }
}
