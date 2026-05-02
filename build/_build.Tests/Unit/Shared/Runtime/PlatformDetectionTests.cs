using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Build.Tests.Fixtures;
using Cake.Core;

namespace Build.Tests.Unit.Shared.Runtime;

public class PlatformDetectionTests
{
    [Test]
    [Arguments("win-x64", PlatformFamily.Windows)]
    [Arguments("win-x86", PlatformFamily.Windows)]
    [Arguments("win-arm64", PlatformFamily.Windows)]
    [Arguments("linux-x64", PlatformFamily.Linux)]
    [Arguments("linux-arm64", PlatformFamily.Linux)]
    [Arguments("osx-x64", PlatformFamily.OSX)]
    [Arguments("osx-arm64", PlatformFamily.OSX)]
    public async Task RuntimeProfile_Should_Detect_Correct_Platform_Family(string rid, PlatformFamily expected)
    {
        var profile = rid switch
        {
            var r when r.StartsWith("win-", StringComparison.Ordinal) => RuntimeProfileFixture.CreateWindows(rid: r),
            var r when r.StartsWith("linux-", StringComparison.Ordinal) => RuntimeProfileFixture.CreateLinux(rid: r),
            var r when r.StartsWith("osx-", StringComparison.Ordinal) => RuntimeProfileFixture.CreateMacOS(rid: r),
            _ => throw new ArgumentException($"Unexpected RID: {rid}", nameof(rid)),
        };

        await Assert.That(profile.PlatformFamily).IsEqualTo(expected);
    }

    [Test]
    public async Task RuntimeProfile_Should_Expose_Rid_And_Triplet()
    {
        var profile = RuntimeProfileFixture.CreateWindows(rid: "win-x64", triplet: "x64-windows-hybrid");
        await Assert.That(profile.Rid).IsEqualTo("win-x64");
        await Assert.That(profile.Triplet).IsEqualTo("x64-windows-hybrid");
    }

    [Test]
    public async Task RuntimeProfile_Should_Throw_When_Rid_Is_Unsupported()
    {
        var runtimeInfo = new RuntimeInfo
        {
            Rid = "freebsd-x64",
            Triplet = "x64-freebsd",
            Strategy = "pure-dynamic",
            Runner = "freebsd-latest",
        };

        await Assert.That(() =>
        {
            _ = new RuntimeProfile(runtimeInfo, RuntimeProfileFixture.SystemArtefacts);
        }).Throws<InvalidOperationException>();
    }
}
