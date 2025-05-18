using System.Text.RegularExpressions;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Build.Tools.Ldd;

/// <summary>
/// The ldd runner for printing shared library dependencies.
/// </summary>
public sealed partial class LddRunner : Tool<LddSettings>
{
    private readonly ICakeEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="LddRunner"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="environment">The environment.</param>
    /// <param name="processRunner">The process runner.</param>
    /// <param name="tools">The tool locator.</param>
    public LddRunner(
        IFileSystem fileSystem,
        ICakeEnvironment environment,
        IProcessRunner processRunner,
        IToolLocator tools) : base(fileSystem, environment, processRunner, tools)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// Gets the name of the tool.
    /// </summary>
    /// <returns>The tool name.</returns>
    protected override string GetToolName() => "ldd";

    /// <summary>
    /// Gets the possible names of the tool executable.
    /// </summary>
    /// <returns>The tool executable name.</returns>
    protected override IEnumerable<string> GetToolExecutableNames() => new[] { "ldd" };

    /// <summary>
    /// Run ldd on the given file and return the output.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The ldd output.</returns>
    public string GetDependencies(LddSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // ldd only works on Linux
        if (!_environment.Platform.IsUnix())
        {
            throw new PlatformNotSupportedException("ldd is only available on Unix/Linux platforms.");
        }

        var args = GetArguments(settings);
        var processOutput = string.Empty;

        Run(settings, args, new ProcessSettings
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            process => processOutput = string.Join(Environment.NewLine, process.GetStandardOutput()));

        return processOutput;
    }

    /// <summary>
    /// Run ldd on the given file and parse the results.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>Dictionary of dependencies with their resolved paths.</returns>
    public IReadOnlyDictionary<string, string> GetDependenciesAsDictionary(LddSettings settings)
    {
        var output = GetDependencies(settings);

        // Typical ldd output lines look like:
        // libssl.so.1.1 => /lib/x86_64-linux-gnu/libssl.so.1.1 (0x00007fa5a69bf000)
        // or for not found libraries:
        // libfoo.so => not found
        var regex = CaptureLddOutput();
        var matches = regex.Matches(output);

        var dependencies = matches
            .Where(match => match.Groups.Count >= 3 && !string.Equals(match.Groups[2].Value, "not found", StringComparison.Ordinal))
            .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value, StringComparer.Ordinal);

        return dependencies;
    }

    private static ProcessArgumentBuilder GetArguments(LddSettings settings)
    {
        var builder = new ProcessArgumentBuilder();

        if (settings.ShowUnused)
        {
            builder.Append("-u");
        }

        if (settings.PerformRelocations)
        {
            builder.Append("-r");
        }

        if (settings.IncludeData)
        {
            builder.Append("-d");
        }

        if (settings.Verbose)
        {
            builder.Append("-v");
        }

        builder.AppendQuoted(settings.FilePath.FullPath);

        return builder;
    }

    [GeneratedRegex(@"^\s*([^\s]+)\s+=>\s+([^\s]+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CaptureLddOutput();
}
