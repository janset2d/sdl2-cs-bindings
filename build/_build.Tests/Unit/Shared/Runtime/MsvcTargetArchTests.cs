using System.Runtime.InteropServices;
using Build.Shared.Runtime;

namespace Build.Tests.Unit.Shared.Runtime;

/// <summary>
/// Coverage for the RID → <see cref="MsvcTargetArch"/> mapping + the
/// <c>vcvarsall.bat</c> argument builder that combines host + target into the
/// single/cross-compile shape (<c>x64</c> vs <c>x64_arm64</c> etc.).
/// </summary>
public sealed class MsvcTargetArchTests
{
    [Test]
    [Arguments("win-x64", MsvcTargetArch.X64)]
    [Arguments("win-x86", MsvcTargetArch.X86)]
    [Arguments("win-arm64", MsvcTargetArch.Arm64)]
    public async Task FromRid_Should_Map_Windows_Rid_To_Target_Arch(string rid, MsvcTargetArch expected)
    {
        await Assert.That(MsvcTargetArchExtensions.FromRid(rid)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("linux-x64")]
    [Arguments("linux-arm64")]
    [Arguments("osx-x64")]
    [Arguments("osx-arm64")]
    [Arguments("win-arm")]
    [Arguments("win-x64-debug")]
    public async Task FromRid_Should_Throw_For_Unsupported_Rid(string rid)
    {
        var thrown = Assert.Throws<PlatformNotSupportedException>(() => MsvcTargetArchExtensions.FromRid(rid));
        await Assert.That(thrown!.Message).Contains("no vcvarsall mapping for RID");
        await Assert.That(thrown.Message).Contains(rid);
    }

    [Test]
    public async Task FromRid_Should_Throw_ArgumentException_For_Null_Or_Whitespace()
    {
        Assert.Throws<ArgumentException>(() => MsvcTargetArchExtensions.FromRid(null!));
        Assert.Throws<ArgumentException>(() => MsvcTargetArchExtensions.FromRid(""));
        Assert.Throws<ArgumentException>(() => MsvcTargetArchExtensions.FromRid("   "));
        await Task.CompletedTask;
    }

    [Test]
    [Arguments(Architecture.X64, MsvcTargetArch.X64, "x64")]
    [Arguments(Architecture.X64, MsvcTargetArch.X86, "x64_x86")]
    [Arguments(Architecture.X64, MsvcTargetArch.Arm64, "x64_arm64")]
    [Arguments(Architecture.X86, MsvcTargetArch.X86, "x86")]
    [Arguments(Architecture.X86, MsvcTargetArch.X64, "x86_x64")]
    [Arguments(Architecture.Arm64, MsvcTargetArch.Arm64, "arm64")]
    [Arguments(Architecture.Arm64, MsvcTargetArch.X64, "arm64_x64")]
    [Arguments(Architecture.Arm64, MsvcTargetArch.X86, "arm64_x86")]
    public async Task ToVcvarsArg_Should_Produce_Host_Target_Combo(
        Architecture hostArch,
        MsvcTargetArch target,
        string expected)
    {
        // Uses the explicit-host-arch internal overload so we can sweep every
        // combination on any CI host — the public overload reads
        // RuntimeInformation.OSArchitecture which pins the test to x64-only.
        await Assert.That(target.ToVcvarsArg(hostArch)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(Architecture.Arm)]
    [Arguments(Architecture.Wasm)]
    public async Task ToVcvarsArg_Should_Throw_For_Unsupported_Host_Architecture(Architecture hostArch)
    {
        var thrown = Assert.Throws<PlatformNotSupportedException>(
            () => MsvcTargetArch.X64.ToVcvarsArg(hostArch));
        await Assert.That(thrown!.Message).Contains("unsupported host architecture");
    }
}
