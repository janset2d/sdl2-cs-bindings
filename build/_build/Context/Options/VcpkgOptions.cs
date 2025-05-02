using System.CommandLine;

namespace Build.Context.Options;

public static class VcpkgOptions
{
    public static readonly Option<DirectoryInfo?> VcpkgDirOption = new(
        aliases: ["--vcpkg-dir"],
        description: "Absolute path to the vcpkg directory. If not specified, defaults to the current directory.")
    {
        IsRequired = false,
    };

    public static readonly Option<List<string>> LibraryOption = new(
        "--library",
        "Specify specific libraries to build (e.g., --library SDL2 --library SDL2_image).")
    {
        Arity = ArgumentArity.ZeroOrMore,
    };

    public static readonly Option<List<string>> RidOption = new(
        "--rid",
        "Specify target Runtime Identifiers (RIDs) for native builds (e.g., --rid win-x64 --rid linux-x64).")
    {
        Arity = ArgumentArity.ZeroOrMore,
    };

    public static readonly Option<bool> UseOverridesOption = new(
        "--use-overrides",
        "Use native binaries from the overrides directory instead of Vcpkg.");
}
