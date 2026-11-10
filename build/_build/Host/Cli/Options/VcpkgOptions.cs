using System.CommandLine;

namespace Build.Host.Cli.Options;

public static class VcpkgOptions
{
    public static readonly Option<DirectoryInfo?> VcpkgDirOption = new("--vcpkg-dir")
    {
        Description = "Absolute path to the vcpkg directory. If not specified, defaults to the current directory.",
    };

    public static readonly Option<DirectoryInfo?> VcpkgInstalledDirOption = new("--vcpkg-installed-dir")
    {
        Description = "Absolute path to the vcpkg_installed directory. If not specified, defaults to the current directory.",
    };

    public static readonly Option<List<string>> LibraryOption = new("--library")
    {
        Description = "Specify specific libraries to build (e.g., --library SDL2 --library SDL2_image).",
        Arity = ArgumentArity.ZeroOrMore,
    };

    public static readonly Option<string?> RidOption = new("--rid")
    {
        Description = "Specify target Runtime Identifiers (RID) for native builds (e.g., --rid win-x64 --rid linux-x64).",
    };
}
