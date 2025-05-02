using System.CommandLine;

namespace Build.Context.Options;

public static class DotNetOptions
{
    public static readonly Option<string> ConfigOption = new(
        aliases: ["--config", "-c"],
        getDefaultValue: () => "Release",
        description: "The build configuration. The default is 'Release'.");
}
