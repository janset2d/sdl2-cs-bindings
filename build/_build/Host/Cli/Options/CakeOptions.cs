using System.CommandLine;
using Cake.Core.Diagnostics;

namespace Build.Host.Cli.Options;

public static class CakeOptions
{
    public static readonly Option<bool> DescriptionOption = new("--description")
    {
        Description = "Shows description about tasks.",
        DefaultValueFactory = _ => false,
    };

    public static readonly Option<bool> DryRunOption = new("--dryrun")
    {
        Description = "Performs a dry run.",
        DefaultValueFactory = _ => false,
    };

    public static readonly Option<bool> ExclusiveOption = new("--exclusive", "-e")
    {
        Description = "Execute a single task without any dependencies",
        DefaultValueFactory = _ => false,
    };

    public static readonly Option<bool> HelpOption = new("--help")
    {
        Description = "Displays additional information about Cake execution",
        DefaultValueFactory = _ => false,
    };

    public static readonly Option<bool> InfoOption = new("--info")
    {
        Description = "Displays additional information about Cake execution",
        DefaultValueFactory = _ => false,
    };

    public static readonly Option<string> TargetOption = new("--target", "-t")
    {
        Description = "The target to run. This is the name of the method in the build app. The default is 'Info'",
        DefaultValueFactory = _ => "Info",
    };

    public static readonly Option<bool> TreeOption = new("--tree")
    {
        Description = "Shows the task dependency tree.",
        DefaultValueFactory = _ => false,
    };

    public static readonly Option<Verbosity?> VerbosityOption = new("--verbosity", "-v")
    {
        Description = "Specifies the amount of information to be displayed (quiet, minimal, normal, verbose, diagnostic)",
    };

    public static readonly Option<bool> VersionOption = new("-version")
    {
        Description = "Displays Cake.Frosting version number",
        DefaultValueFactory = _ => false,
    };

    public static readonly Option<DirectoryInfo?> WorkingPathOption = new("--working", "-w")
    {
        Description = "Sets the working directory.",
    };
}
