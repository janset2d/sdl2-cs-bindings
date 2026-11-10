using Cake.Core.Diagnostics;

namespace Build.Tests.Fixtures;

internal static class CakeHostCommandLineData
{
    public const int CancelledProbeExitCode = 23;
    public const string RootDescription = "Cake build for janset2d/sdl2-cs-bindings";
    public const string Config = "Debug";
    public const string DefaultConfig = "Release";
    public const string Rid = "linux-x64";
    public const string Suffix = "local.probe";
    public const string ExplicitVersions = "sdl2-core=2.32.0,sdl2-image=2.8.0";
    public const string VersionsFile = "artifacts/probe-versions.json";
    public static string RepoRoot => Path.GetFullPath("repo-root");
    public static string VcpkgDir => Path.GetFullPath("vcpkg-root");
    public static string VcpkgInstalledDir => Path.GetFullPath("vcpkg-installed");
    public static string[] Libraries => ["SDL2_image", "SDL2"];
    public static string[] Dlls => ["SDL2_image.dll", "SDL2.dll"];
    public static string[] Scope => ["sdl2-image", "sdl2-core"];
    public static string[] ExplicitVersion => ["sdl2-image=2.8.0", "sdl2-core=2.32.0"];

    public static string[] AllArguments =>
    [
        "--repo-root", RepoRoot, "--config", Config, "--vcpkg-dir", VcpkgDir, "--vcpkg-installed-dir", VcpkgInstalledDir,
        "--library", Libraries[0], "--library", Libraries[1], "--rid", Rid,
        "--dll", Dlls[0], "--dll", Dlls[1], "--suffix", Suffix,
        "--scope", Scope[0], "--scope", Scope[1], "--explicit-version", ExplicitVersion[0], "--explicit-version", ExplicitVersion[1],
        "--explicit-versions", ExplicitVersions, "--versions-file", VersionsFile,
    ];

    public static string[] UserTokens => ["--rid", Rid, "--tree"];
    public const string InvalidVerbosity = "not-a-verbosity";
    public static string[] InvalidVerbosityArguments => ["-v", InvalidVerbosity];
    public static string[] InvalidVerbosityErrors => [$"Invalid Cake verbosity '{InvalidVerbosity}'."];
    public static IEnumerable<(string Value, Verbosity Expected)> LowercaseVerbosity() =>
    [
        ("quiet", Verbosity.Quiet), ("q", Verbosity.Quiet), ("m", Verbosity.Minimal),
        ("n", Verbosity.Normal), ("v", Verbosity.Verbose), ("d", Verbosity.Diagnostic),
    ];
    public static string[] CakeHelp => ["--help"];
    public static string[] WrapperHelpAliases => ["-h", "-?"];
    public static string[] OptionNames =>
    [
        "--description", "--dryrun", "--exclusive", "--help", "--info", "--target", "--tree", "--verbosity", "-version", "--working",
        "--repo-root", "--config", "--vcpkg-dir", "--vcpkg-installed-dir", "--library", "--rid", "--suffix", "--scope", "--explicit-version",
        "--explicit-versions", "--versions-file", "--dll", "--version", "--help",
    ];
}
