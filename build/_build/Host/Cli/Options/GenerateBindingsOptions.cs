using System.CommandLine;

namespace Build.Host.Cli.Options;

public static class GenerateBindingsOptions
{
    public static readonly Option<bool> EvidenceOption = new("--evidence")
    {
        Description = "Write non-mutating binding evidence with GenerateBindings; no generation or external process is run.",
    };
}
