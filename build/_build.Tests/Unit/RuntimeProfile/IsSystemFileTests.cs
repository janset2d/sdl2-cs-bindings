using Build.Tests.Fixtures;
using Cake.Core.IO;

namespace Build.Tests.Unit.RuntimeProfile;

public class IsSystemFileTests
{
    [Test]
    [Arguments("kernel32.dll", true)]
    [Arguments("ntdll.dll", true)]
    [Arguments("user32.dll", true)]
    [Arguments("advapi32.dll", true)]
    [Arguments("ucrtbase.dll", true)]
    [Arguments("SDL2.dll", false)]
    [Arguments("SDL2_image.dll", false)]
    [Arguments("zlib1.dll", false)]
    [Arguments("libpng16.dll", false)]
    public async Task IsSystemFile_Should_Identify_Windows_System_Dlls(string fileName, bool expected)
    {
        var profile = RuntimeProfileFixture.CreateWindows();
        var result = profile.IsSystemFile(new FilePath(fileName));
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("KERNEL32.DLL", true)]
    [Arguments("Kernel32.Dll", true)]
    [Arguments("UCRTBASE.DLL", true)]
    public async Task IsSystemFile_Should_Be_Case_Insensitive_On_Windows(string fileName, bool expected)
    {
        var profile = RuntimeProfileFixture.CreateWindows();
        var result = profile.IsSystemFile(new FilePath(fileName));
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("msvcp140.dll", true)]
    [Arguments("msvcp140_1.dll", true)]
    [Arguments("vcruntime140.dll", true)]
    [Arguments("vcruntime140_1.dll", true)]
    [Arguments("api-ms-win-crt-runtime-l1-1-0.dll", true)]
    public async Task IsSystemFile_Should_Match_Wildcard_Patterns_On_Windows(string fileName, bool expected)
    {
        var profile = RuntimeProfileFixture.CreateWindows();
        var result = profile.IsSystemFile(new FilePath(fileName));
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("libc.so.6", true)]
    [Arguments("libm.so.6", true)]
    [Arguments("libpthread.so.0", true)]
    [Arguments("libstdc++.so.6", true)]
    [Arguments("libgcc_s.so.1", true)]
    [Arguments("libSDL2-2.0.so.0", false)]
    [Arguments("libSDL2_image-2.0.so.0", false)]
    [Arguments("libz.so.1", false)]
    public async Task IsSystemFile_Should_Identify_Linux_System_Libraries(string fileName, bool expected)
    {
        var profile = RuntimeProfileFixture.CreateLinux();
        var result = profile.IsSystemFile(new FilePath(fileName));
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("libSystem.B.dylib", true)]
    [Arguments("libobjc.A.dylib", true)]
    [Arguments("Cocoa.framework", true)]
    [Arguments("Metal.framework", true)]
    [Arguments("libSDL2.dylib", false)]
    [Arguments("libfreetype.6.dylib", false)]
    public async Task IsSystemFile_Should_Identify_MacOS_System_Libraries(string fileName, bool expected)
    {
        var profile = RuntimeProfileFixture.CreateMacOS();
        var result = profile.IsSystemFile(new FilePath(fileName));
        await Assert.That(result).IsEqualTo(expected);
    }
}
