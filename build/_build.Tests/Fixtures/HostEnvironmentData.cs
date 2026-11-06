using Cake.Core.IO;

namespace Build.Tests.Fixtures;

public static class HostEnvironmentData
{
    public const string Installation = "C:/Visual Studio";
    public const string Batch = Installation + "/VC/Auxiliary/Build/vcvarsall.bat";
    public const string CapturedEnvironment = "UNCHANGED=original\r\nCHANGED=after\r\nUNICODE=İstanbul\r\n=hidden=value\r\ninvalid key=value\r\nnoise\r\n";
    public const string ParentMarker = "C:/VC/Tools/";
    public const string CaptureCommand = "/s /c \"\"C:/Visual Studio/VC/Auxiliary/Build/vcvarsall.bat\" x64 >nul && set\"";
    public const string BatchProbeSource = "@echo off\r\nset JANSET_BATCH_PROBE=captured\r\n";
    public const string Utf8Payload = "İstanbul";
    public const string Utf8ChildSource = """
        #:property TargetFramework=net10.0
        #:property PublishAot=false
        byte[] payload = [0xC4, 0xB0, 0x73, 0x74, 0x61, 0x6E, 0x62, 0x75, 0x6C];
        using var output = Console.OpenStandardOutput();
        using var error = Console.OpenStandardError();
        output.Write(payload);
        error.Write(payload);
        """;

    public static string CacheRoot => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "janset-sdl2", "dotnet-runtime-env");
    public static string RuntimeRoot => System.IO.Path.Combine(CacheRoot, "x86");
    public static string InstallScript => System.IO.Path.Combine(CacheRoot, "dotnet-install.ps1");
    public static string PowerShell => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");

    public static FakeCakeWorld MsvcWorld(int exitCode = 0, string error = "", string? installation = null)
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithToolPath(new FilePath("C:/tools/vswhere.exe"))
            .WithProcessResult("vswhere.exe", 0, installation ?? Installation)
            .WithTextFile((installation ?? Installation) + "/VC/Auxiliary/Build/vcvarsall.bat", "batch")
            .WithProcessResult("cmd.exe", exitCode, CapturedEnvironment, error);
        world.Environment.SetEnvironmentVariable("UNCHANGED", "original");
        world.Environment.SetEnvironmentVariable("CHANGED", "before");
        return world;
    }

    public static FakeCakeWorld RuntimeWorld(int exitCode = 0, string output = "", string error = "")
    {
        return FakeCakeWorld.CreateWindows()
            .WithTextFile(InstallScript, "installer")
            .WithTextFile(PowerShell, "powershell")
            .WithProcessResult("pwsh.exe", exitCode, output, error);
    }
}
