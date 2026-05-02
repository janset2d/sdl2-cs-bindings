using System.Collections.Immutable;
using Build.Features.Diagnostics;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Testing;
using NSubstitute;

namespace Build.Tests.Unit.Features.Diagnostics;

public sealed class InspectHarvestedDependenciesPipelineTests
{
    [Test]
    public async Task RunAsync_Should_Throw_When_Platform_Is_Unknown()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithRid("win-x64")
            .BuildContextWithHandles();

        // Use an out-of-range RuntimeFamily value to exercise the default-switch path
        // (RuntimeFamily has no Unknown member by design — Shared/ vocabulary is closed-set
        // per ADR-004 §2.6).
        var profile = CreateRuntimeProfile("win-x64", (RuntimeFamily)999);
        var vcpkgConfig = repo.BuildContext.Options.Vcpkg;
        var runner = new InspectHarvestedDependenciesPipeline(
            repo.CakeContext, new FakeLog(), repo.Paths, profile, ManifestFixture.CreateTestManifestConfig(), vcpkgConfig);

        var ex = await Assert.That(() => runner.RunAsync()).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("OS key");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Harvest_Native_Directory_Missing()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Unix, repoRoot: "/repo")
            .WithRid("linux-x64")
            .BuildContextWithHandles();

        var profile = CreateRuntimeProfile("linux-x64", RuntimeFamily.Linux);
        var vcpkgConfig = repo.BuildContext.Options.Vcpkg;
        var runner = new InspectHarvestedDependenciesPipeline(
            repo.CakeContext, new FakeLog(), repo.Paths, profile, ManifestFixture.CreateTestManifestConfig(), vcpkgConfig);

        var ex = await Assert.That(() => runner.RunAsync()).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("missing");
        await Assert.That(ex.Message).Contains("--target Harvest --rid linux-x64");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Unix_Tarball_Missing()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Unix, repoRoot: "/repo")
            .WithRid("linux-x64")
            .WithLibraries("SDL2")
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/linux-x64/native/.placeholder", "")
            .BuildContextWithHandles();

        var profile = CreateRuntimeProfile("linux-x64", RuntimeFamily.Linux);
        var vcpkgConfig = repo.BuildContext.Options.Vcpkg;
        var runner = new InspectHarvestedDependenciesPipeline(
            repo.CakeContext, new FakeLog(), repo.Paths, profile, ManifestFixture.CreateTestManifestConfig(), vcpkgConfig);

        var ex = await Assert.That(() => runner.RunAsync()).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("native.tar.gz");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Requested_Library_Is_Not_In_Manifest()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Unix, repoRoot: "/repo")
            .WithRid("linux-x64")
            .WithLibraries("SDL2_not_real")
            .BuildContextWithHandles();

        var profile = CreateRuntimeProfile("linux-x64", RuntimeFamily.Linux);
        var vcpkgConfig = repo.BuildContext.Options.Vcpkg;
        var runner = new InspectHarvestedDependenciesPipeline(
            repo.CakeContext, new FakeLog(), repo.Paths, profile, ManifestFixture.CreateTestManifestConfig(), vcpkgConfig);

        var ex = await Assert.That(() => runner.RunAsync()).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("SDL2_not_real");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Primary_Binary_Missing_From_Extracted_Payload()
    {
        // Windows path: no extraction, scanner would be dumpbin but we fail earlier at primary resolve
        // because the harvest native dir has no matching file.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows, repoRoot: "C:/repo")
            .WithRid("win-x64")
            .WithLibraries("SDL2")
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/win-x64/native/README.txt", "no primary here")
            .BuildContextWithHandles();

        var profile = CreateRuntimeProfile("win-x64", RuntimeFamily.Windows);
        var vcpkgConfig = repo.BuildContext.Options.Vcpkg;
        var runner = new InspectHarvestedDependenciesPipeline(
            repo.CakeContext, new FakeLog(), repo.Paths, profile, ManifestFixture.CreateTestManifestConfig(), vcpkgConfig);

        var ex = await Assert.That(() => runner.RunAsync()).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("no primary binary matched");
    }

    [Test]
    public async Task RunAsync_Should_Reject_Library_With_No_Primary_Binary_Entry_For_Current_Os()
    {
        // Seed a manifest whose library lacks a Windows entry, then invoke Inspect on win-x64 — should
        // throw at ResolvePatterns.
        var manifest = ManifestFixture.CreateTestManifestConfig() with
        {
            LibraryManifests = ImmutableList.Create(
                ManifestFixture.CreateTestCoreLibrary() with
                {
                    PrimaryBinaries = ImmutableList.Create(
                        new PrimaryBinary { Os = "Linux", Patterns = ImmutableList.Create("libSDL2*") }),
                }),
        };

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows, repoRoot: "C:/repo")
            .WithRid("win-x64")
            .WithLibraries("SDL2")
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/win-x64/native/SDL2.dll", "dll")
            .BuildContextWithHandles();

        var profile = CreateRuntimeProfile("win-x64", RuntimeFamily.Windows);
        var vcpkgConfig = repo.BuildContext.Options.Vcpkg;
        var runner = new InspectHarvestedDependenciesPipeline(
            repo.CakeContext, new FakeLog(), repo.Paths, profile, manifest, vcpkgConfig);

        var ex = await Assert.That(() => runner.RunAsync()).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("no primary_binaries entry for OS 'Windows'");
    }

    private static IRuntimeProfile CreateRuntimeProfile(string rid, RuntimeFamily platform)
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.Rid.Returns(rid);
        profile.Family.Returns(platform);
        return profile;
    }
}
