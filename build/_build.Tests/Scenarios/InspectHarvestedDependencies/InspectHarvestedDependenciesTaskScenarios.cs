using System.Collections.Immutable;
using Build.Repositories;
using Build.Shared.Manifest;
using Build.Targets.InspectHarvestedDependencies;
using Build.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Scenarios.InspectHarvestedDependencies;

public sealed class InspectHarvestedDependenciesTaskScenarios
{
    [Test]
    public async Task RunAsync_Should_Run_Ldd_Against_Extracted_Payload_On_Linux()
    {
        var world = FakeCakeWorldV2.CreateLinux()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithLibraries("SDL2")
            .WithProcessResult("tar", exitCode: 0, stdOut: "")
            .WithProcessSideEffect("tar", w =>
                w.WithTextFile("artifacts/temp/inspect/linux-x64/SDL2/libSDL2-2.0.so.0", "fake binary"))
            .WithProcessResult("ldd", exitCode: 0,
                stdOut: "    libc.so.6 => /lib/x86_64-linux-gnu/libc.so.6 (0x...)\n")
            .WithToolPath("tar", "/usr/bin/tar")
            .WithToolPath("ldd", "/usr/bin/ldd")
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/linux-x64/native/native.tar.gz", "fake archive");

        var host = CreateHost(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.ProcessInvocations).Contains(p => p.Command.FullPath.Contains("tar", StringComparison.Ordinal));
        await Assert.That(world.ProcessInvocations).Contains(p => p.Command.FullPath.Contains("ldd", StringComparison.Ordinal));
    }

    [Test]
    public async Task RunAsync_Should_Run_Dumpbin_Against_Native_Dir_On_Windows_Without_Extraction()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithLibraries("SDL2")
            .WithProcessResult("dumpbin.exe", exitCode: 0, stdOut: "    KERNEL32.dll\n")
            .WithToolPath("C:/dumpbin/dumpbin.exe")
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/win-x64/native/SDL2.dll", "fake binary");

        var host = CreateHost(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.ProcessInvocations).Contains(p => p.Command.FullPath.Contains("dumpbin", StringComparison.Ordinal));
        await Assert.That(world.ProcessInvocations).DoesNotContain(p => p.Command.FullPath.Contains("tar", StringComparison.Ordinal));
    }

    [Test]
    public async Task RunAsync_Should_Run_Otool_Against_Extracted_Payload_On_Macos()
    {
        var world = FakeCakeWorldV2.CreateOsx()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithLibraries("SDL2")
            .WithProcessResult("tar", exitCode: 0, stdOut: "")
            .WithProcessSideEffect("tar", w =>
                w.WithTextFile("artifacts/temp/inspect/osx-x64/SDL2/libSDL2-2.0.0.dylib", "fake binary"))
            .WithProcessResult("otool", exitCode: 0,
                stdOut: "/path/libSDL2.dylib:\n\t/usr/lib/libSystem.B.dylib (compatibility version 1.0.0, current version 1.0.0)\n")
            .WithToolPath("tar", "/usr/bin/tar")
            .WithToolPath("otool", "/usr/bin/otool")
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/osx-x64/native/native.tar.gz", "fake archive");

        var host = CreateHost(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.ProcessInvocations).Contains(p => p.Command.FullPath.Contains("otool", StringComparison.Ordinal));
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Harvest_Native_Directory_Missing()
    {
        var world = FakeCakeWorldV2.CreateLinux()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithLibraries("SDL2");

        var host = CreateHost(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("missing");
        await Assert.That(result.Exception.Message).Contains("--target Harvest --rid linux-x64");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Unix_Tarball_Missing()
    {
        var world = FakeCakeWorldV2.CreateLinux()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithLibraries("SDL2")
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/linux-x64/native/.placeholder", "");

        var host = CreateHost(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("native.tar.gz");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Requested_Library_Is_Not_In_Manifest()
    {
        var world = FakeCakeWorldV2.CreateLinux()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithLibraries("SDL2_not_real");

        var host = CreateHost(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("SDL2_not_real");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Primary_Binary_Missing_From_Extracted_Payload()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithLibraries("SDL2")
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/win-x64/native/README.txt", "no primary here");

        var host = CreateHost(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("no primary binary matched");
    }

    [Test]
    public async Task RunAsync_Should_Reject_Library_With_No_PrimaryBinary_Entry_For_Current_Os()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig() with
        {
            LibraryManifests = ImmutableList.Create(
                ManifestFixture.CreateTestCoreLibrary() with
                {
                    PrimaryBinaries = ImmutableList.Create(
                        new PrimaryBinary { Os = "Linux", Patterns = ImmutableList.Create("libSDL2*") }),
                }),
        };

        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(manifest)
            .WithLibraries("SDL2")
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/win-x64/native/SDL2.dll", "dll");

        var host = CreateHost(world);
        var result = await host.RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("no primary_binaries entry for OS 'Windows'");
    }

    private static TargetTestHostV2<InspectHarvestedDependenciesTask> CreateHost(FakeCakeWorldV2 world)
    {
        return new TargetTestHostV2<InspectHarvestedDependenciesTask>(world)
            .WithServices(services =>
            {
                services.AddRepositories();
                services.AddInspectHarvestedDependenciesTarget();
            });
    }
}
