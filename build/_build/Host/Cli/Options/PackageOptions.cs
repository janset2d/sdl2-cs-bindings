using System.CommandLine;

namespace Build.Host.Cli.Options;

public static class PackageOptions
{
    public static readonly Option<string> SourceOption = new(
        aliases: ["--source"],
        getDefaultValue: () => "local",
        description: "Artifact source profile for local setup (local|remote|release).")
    {
        IsRequired = false,
    };
}
