using Cake.Core.IO;

namespace Build.Tests.Fixtures;

public static class HostEnvironmentData
{
    public const string Installation = "C:/Visual Studio";
    public const string Batch = Installation + "/VC/Auxiliary/Build/vcvarsall.bat";
    public const string CapturedEnvironment = "UNCHANGED=original\r\nCHANGED=after\r\nUNICODE=İstanbul\r\n=hidden=value\r\ninvalid key=value\r\nnoise\r\n";
    public const string ParentMarker = "C:/VC/Tools/";

    public static string CacheRoot => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "janset-sdl2", "dotnet-runtime-env");
    public static string RuntimeRoot => System.IO.Path.Combine(CacheRoot, "x86");
    public static string InstallScript => System.IO.Path.Combine(CacheRoot, "dotnet-install.ps1");
    public static string PowerShell => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");

    public static FakeCakeWorld MsvcWorld(int exitCode = 0, string error = "")
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithToolPath(new FilePath("C:/tools/vswhere.exe"))
            .WithProcessResult("vswhere.exe", 0, Installation)
            .WithTextFile(Batch, "batch")
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
