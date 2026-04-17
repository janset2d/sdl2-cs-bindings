using System.CommandLine;

namespace Build.Context.Options;

public static class PackageOptions
{
    public static readonly Option<List<string>> FamilyOption = new(
        "--family",
        "Specify one or more package families to pack (e.g., --family sdl2-core --family sdl2-image).")
    {
        Arity = ArgumentArity.ZeroOrMore,
    };

    public static readonly Option<string?> FamilyVersionOption = new(
        "--family-version",
        "Explicit SemVer used for the selected package family or families during local packaging.");
}
