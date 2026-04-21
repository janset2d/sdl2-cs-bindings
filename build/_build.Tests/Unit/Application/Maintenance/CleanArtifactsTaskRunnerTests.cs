using Build.Application.Maintenance;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Application.Maintenance;

public sealed class CleanArtifactsTaskRunnerTests
{
    [Test]
    public async Task RunAsync_Should_Wipe_All_Target_Roots_When_They_Exist()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/win-x64/native/SDL2.dll", "payload")
            .WithTextFile("artifacts/packages/Janset.SDL2.Core.2.32.0-local.1.nupkg", "nupkg")
            .WithTextFile("artifacts/package-consumer-smoke/linux-x64/bin/Debug/a.dll", "bin")
            .WithTextFile("artifacts/test-results/smoke/smoke.trx", "trx")
            .WithTextFile("artifacts/harvest-staging/SDL2-win-x64/runtimes/win-x64/native/SDL2.dll", "staging")
            .WithTextFile("artifacts/temp/inspect/linux-x64/SDL2/libSDL2-2.0.so", "inspect")
            .WithTextFile("artifacts/matrix/runtimes.json", "{\"all\":[]}")
            .WithTextFile("tests/smoke-tests/native-smoke/build/win-x64/native-smoke.exe", "exe")
            .BuildContextWithHandles();

        var runner = new CleanArtifactsTaskRunner(repo.CakeContext, new Cake.Testing.FakeLog(), repo.Paths);

        await runner.RunAsync();

        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2/runtimes/win-x64/native/SDL2.dll")).IsFalse();
        await Assert.That(repo.Exists("artifacts/packages/Janset.SDL2.Core.2.32.0-local.1.nupkg")).IsFalse();
        await Assert.That(repo.Exists("artifacts/package-consumer-smoke/linux-x64/bin/Debug/a.dll")).IsFalse();
        await Assert.That(repo.Exists("artifacts/test-results/smoke/smoke.trx")).IsFalse();
        await Assert.That(repo.Exists("artifacts/harvest-staging/SDL2-win-x64/runtimes/win-x64/native/SDL2.dll")).IsFalse();
        await Assert.That(repo.Exists("artifacts/temp/inspect/linux-x64/SDL2/libSDL2-2.0.so")).IsFalse();
        await Assert.That(repo.Exists("artifacts/matrix/runtimes.json")).IsFalse();
        await Assert.That(repo.Exists("tests/smoke-tests/native-smoke/build/win-x64/native-smoke.exe")).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Not_Touch_Vcpkg_Installed_Directory()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Unix, repoRoot: "/repo")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/lib/libSDL2-2.0.so.0.3000.0", "keep-me")
            .WithTextFile("artifacts/harvest_output/SDL2/rid-status/linux-x64.json", "{}")
            .BuildContextWithHandles();

        var runner = new CleanArtifactsTaskRunner(repo.CakeContext, new Cake.Testing.FakeLog(), repo.Paths);

        await runner.RunAsync();

        await Assert.That(repo.Exists("vcpkg_installed/x64-linux-hybrid/lib/libSDL2-2.0.so.0.3000.0")).IsTrue();
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2/rid-status/linux-x64.json")).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Be_Idempotent_When_Targets_Do_Not_Exist()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var runner = new CleanArtifactsTaskRunner(repo.CakeContext, new Cake.Testing.FakeLog(), repo.Paths);

        await runner.RunAsync();
        await runner.RunAsync();

        await Assert.That(repo.Exists("artifacts/harvest_output/anything.json")).IsFalse();
    }
}
