using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Path = System.IO.Path;

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
    protected override IEnumerable<string> GetToolExecutableNames() => ["ldd"];

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
                RedirectStandardError = true,
            },
            process => processOutput = string.Join(Environment.NewLine, process.GetStandardOutput()));

        return processOutput;
    }

    public IReadOnlyDictionary<string, string> GetDependenciesAsDictionary(LddSettings settings)
    {
        var output = GetDependencies(settings);
        var dependencies = new Dictionary<string, string>(StringComparer.Ordinal);

        var lines = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                continue;
            }

            // Handle redirected libraries: "libname.so => /path/to/lib.so (0x...)"
            if (trimmedLine.Contains(" => ", StringComparison.Ordinal))
            {
                var parts = trimmedLine.Split(" => ", 2);
                if (parts.Length < 2)
                {
                    continue;
                }

                var libName = parts[0].Trim();
                var remainingPart = parts[1].Trim();

                // Handle "not found" case
                if (remainingPart.Equals("not found", StringComparison.Ordinal))
                {
                    continue;
                }

                // Remove the address part "(0x...)" if present
                var addressIndex = remainingPart.LastIndexOf(" (0x", StringComparison.Ordinal);
                var libPath = addressIndex > 0
                    ? remainingPart[..addressIndex].Trim()
                    : remainingPart;

                dependencies[libName] = libPath;
                continue;
            }

            // Handle direct dependencies without redirection: "/lib64/ld-linux-x86-64.so.2 (0x...)"
            var directAddressIndex = trimmedLine.LastIndexOf(" (0x", StringComparison.Ordinal);
            if (directAddressIndex > 0)
            {
                var libPath = trimmedLine[..directAddressIndex].Trim();
                var libName = Path.GetFileName(libPath);

                dependencies[libName] = libPath;
            }
        }

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
}
