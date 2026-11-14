internal sealed record OracleOptions(
    bool SelfTest,
    bool ShowHelp,
    string RepoRoot,
    IReadOnlyList<string> Families,
    bool WriteReport,
    bool Stdout,
    string? ParseError)
{
    public const string HelpText = """
usage: dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- [options]

options:
  --self-test          Run oracle self-tests.
  --family <id>       Process a family. Can be repeated.
  --repo-root <path>  Override repository root. Defaults by probing upward for build/manifest.json.
  --write-report      Write Markdown evidence report under spikes/binding-generators/output/reports/.
  --stdout            Print source status blocks when --write-report is specified.
  --help              Show this help.
""";

    public static OracleOptions Parse(string[] args)
        => Parse(args, Directory.GetCurrentDirectory());

    public static OracleOptions Parse(string[] args, string startDirectory)
    {
        var selfTest = false;
        var help = false;
        var repoRoot = FindRepoRoot(startDirectory);
        var families = new List<string>();
        var writeReport = false;
        var stdout = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--self-test", StringComparison.OrdinalIgnoreCase))
            {
                selfTest = true;
                continue;
            }

            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                help = true;
                continue;
            }

            if (arg.Equals("--family", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return CreateParseError("Missing value for --family.");
                }

                families.Add(args[++i]);
                continue;
            }

            if (arg.Equals("--repo-root", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return CreateParseError("Missing value for --repo-root.");
                }

                repoRoot = args[++i];
                continue;
            }

            if (arg.Equals("--write-report", StringComparison.OrdinalIgnoreCase))
            {
                writeReport = true;
                continue;
            }

            if (arg.Equals("--stdout", StringComparison.OrdinalIgnoreCase))
            {
                stdout = true;
                continue;
            }

            return arg.StartsWith("--", StringComparison.Ordinal)
                ? CreateParseError($"Unknown option: {arg}.")
                : CreateParseError($"Unknown argument: {arg}.");
        }

        return new OracleOptions(selfTest, help, repoRoot, families, writeReport, stdout || !writeReport, null);
    }

    private static OracleOptions CreateParseError(string error)
        => new(false, false, FindRepoRoot(Directory.GetCurrentDirectory()), [], false, false, error);

    private static string FindRepoRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "build", "manifest.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(startDirectory);
    }
}
