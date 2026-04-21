using Build.Application.Harvesting;
using Build.Domain.Runtime;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;

namespace Build.Tests.Unit.Application.Harvesting;

/// <summary>
/// Strategy A coverage (fake-filesystem precondition paths only) per the Slice D plan:
/// we do NOT mock Cake.CMake or the native-smoke process invocation here. Those are
/// validated end-to-end by the Linux witness. These tests guard the runner's input handling
/// — library resolution + harvest-output preconditions — which is where the fail-fast
/// operator UX lives.
/// </summary>
public sealed class NativeSmokeTaskRunnerTests
{
    [Test]
    public async Task RunAsync_Should_Throw_When_Harvest_Output_Missing_For_Specified_Library()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Unix, repoRoot: "/repo")
            .WithRid("linux-x64")
            .WithLibraries("SDL2")
            .BuildContextWithHandles();

        var profile = CreateRuntimeProfile("linux-x64", PlatformFamily.Linux);
        var runner = new NativeSmokeTaskRunner(
            repo.CakeContext, new FakeLog(), repo.Paths, profile, ManifestFixture.CreateTestManifestConfig());

        var ex = await Assert.That(() => runner.RunAsync(repo.BuildContext)).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("is missing for library 'SDL2'");
        await Assert.That(ex.Message).Contains("--target Harvest --rid linux-x64");
    }

    [Test]
    public async Task RunAsync_Should_Point_To_Harvest_Target_When_Native_Dir_Is_Absent()
    {
        // Seed a sibling subdir only; the expected runtimes/<rid>/native/ tree never gets created,
        // so the precondition surfaces as "missing" with the Harvest remediation hint.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Unix, repoRoot: "/repo")
            .WithRid("linux-x64")
            .WithLibraries("SDL2")
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/linux-x64/other/other-file.txt", "sibling")
            .BuildContextWithHandles();

        var profile = CreateRuntimeProfile("linux-x64", PlatformFamily.Linux);
        var runner = new NativeSmokeTaskRunner(
            repo.CakeContext, new FakeLog(), repo.Paths, profile, ManifestFixture.CreateTestManifestConfig());

        var ex = await Assert.That(() => runner.RunAsync(repo.BuildContext)).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("--target Harvest --rid linux-x64");
    }

    [Test]
    public async Task RunAsync_Should_Reject_Specified_Library_Not_Present_In_Manifest()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Unix, repoRoot: "/repo")
            .WithRid("linux-x64")
            .WithLibraries("SDL2_ghost")
            .BuildContextWithHandles();

        var profile = CreateRuntimeProfile("linux-x64", PlatformFamily.Linux);
        var runner = new NativeSmokeTaskRunner(
            repo.CakeContext, new FakeLog(), repo.Paths, profile, ManifestFixture.CreateTestManifestConfig());

        var ex = await Assert.That(() => runner.RunAsync(repo.BuildContext)).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("SDL2_ghost");
        await Assert.That(ex.Message).Contains("--library");
    }

    [Test]
    public async Task RunAsync_Should_Accept_Any_Rid_Without_Hardcoded_Cap()
    {
        // Regression guard: the earlier 3-RID cap (win-x64/linux-x64/osx-x64) was removed in the
        // symmetric 7-RID pivot. The runner must no longer reject RIDs by name — only downstream
        // CMake preset resolution arbitrates (out of scope for strategy A). Here we simply assert
        // that a PA-2 RID gets past RID inspection and fails later at the harvest-output
        // precondition (proving no cap rejection earlier).
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows, repoRoot: "C:/repo")
            .WithRid("win-arm64")
            .WithLibraries("SDL2")
            .BuildContextWithHandles();

        var profile = CreateRuntimeProfile("win-arm64", PlatformFamily.Windows);
        var runner = new NativeSmokeTaskRunner(
            repo.CakeContext, new FakeLog(), repo.Paths, profile, ManifestFixture.CreateTestManifestConfig());

        var ex = await Assert.That(() => runner.RunAsync(repo.BuildContext)).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("missing for library 'SDL2'");
        // The error must NOT be the legacy "NativeSmoke supports only …" cap wording.
        await Assert.That(ex.Message).DoesNotContain("supports only");
    }

    private static IRuntimeProfile CreateRuntimeProfile(string rid, PlatformFamily platform)
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.Rid.Returns(rid);
        profile.PlatformFamily.Returns(platform);
        return profile;
    }
}
