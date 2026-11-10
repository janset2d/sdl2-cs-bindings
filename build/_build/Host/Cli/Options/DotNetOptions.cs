using System.CommandLine;

namespace Build.Host.Cli.Options;

public static class DotNetOptions
{
    public static readonly Option<string> ConfigOption = new("--config", "-c")
    {
        Description = "The build configuration. The default is 'Release'.",
        DefaultValueFactory = _ => "Release",
    };
}
