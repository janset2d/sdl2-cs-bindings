using System.CommandLine;
using Cake.Core.Diagnostics;

namespace Build.Context.Options;

public static class CakeOptions
{
    public static readonly Option<bool> DescriptionOption = new(
        "--description",
        description: "Shows description about tasks.",
        getDefaultValue: () => false);

    public static readonly Option<bool> DryRunOption = new(
        "--dryrun",
        description: "Performs a dry run.",
        getDefaultValue: () => false);

    public static readonly Option<bool> ExclusiveOption = new(
        aliases: ["--exclusive", "-e"],
        description: "Execute a single task without any dependencies",
        getDefaultValue: () => false);

    public static readonly Option<bool> HelpOption = new(
        "--help",
        description: "Displays additional information about Cake execution",
        getDefaultValue: () => false);

    public static readonly Option<bool> InfoOption = new(
        "--info",
        description: "Displays additional information about Cake execution",
        getDefaultValue: () => false);

    public static readonly Option<string> TargetOption = new(
        aliases: ["--target", "-t"],
        getDefaultValue: () => "Info",
        description: "The target to run. This is the name of the method in the build app. The default is 'Info'");

    public static readonly Option<bool> TreeOption = new(
        "--tree",
        description: "Shows the task dependency tree.",
        getDefaultValue: () => false);

    public static readonly Option<Verbosity?> VerbosityOption = new(
        aliases: ["--verbosity", "-v"],
        description: "Specifies the amount of information to be displayed (quiet, minimal, normal, verbose, diagnostic)");

    public static readonly Option<bool> VersionOption = new(
        "-version",
        description: "Displays Cake.Frosting version number",
        getDefaultValue: () => false);

    public static readonly Option<DirectoryInfo?> WorkingPathOption = new(
        aliases: ["--working", "-w"],
        description: "Sets the working directory.");
}
