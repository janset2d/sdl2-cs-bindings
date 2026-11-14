using System.CommandLine;

namespace Build.Host.Cli.Options;

public static class GenerateBindingsOptions
{
    public static readonly Option<bool> CheckOption = new("--check")
    {
        Description = "Read-only validation of committed generation stamps and generated files.",
    };

    public static readonly Option<bool> RequireHeadersOption = new("--require-headers")
    {
        Description = "Require the recorded triplet's installed headers during the stamp check.",
    };

    public static readonly Option<bool> ExecuteOption = new("--execute")
    {
        Description = "Generate complete binding families with GenerateBindings; absent means read-only planning.",
    };

    public static readonly Option<bool> PlatformHeaderShimsOption = new("--platform-header-shims")
    {
        Description = "Use synthetic platform-header shims for Windows-local binding-generation iteration.",
    };

    public static readonly Option<bool> EvidenceOption = new("--evidence")
    {
        Description = "Write non-mutating binding evidence with GenerateBindings; no generation or external process is run.",
    };
}
