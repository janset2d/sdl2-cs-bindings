#pragma warning disable S2737, CA1031

using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Cake.Common.Diagnostics;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Path = System.IO.Path;

namespace Build.Tools.Otool;

/// <summary>
/// The otool runner used to execute otool commands.
/// </summary>
public sealed partial class OtoolRunner : Tool<OtoolSettings>
{
    private readonly ICakeContext _context;

    [GeneratedRegex(@"(?<framework>[^/]+\.framework)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 500)]
    private static partial Regex FrameworkNameExtractor();

    [GeneratedRegex(@"^\s*(?<path>.+?)\s+\(compatibility\s+version\s+.+?\)$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 500)]
    private static partial Regex GetLibraryCompatibilityVersion();

    /// <summary>
    /// Initializes a new instance of the <see cref="OtoolRunner"/> class.
    /// </summary>
    public OtoolRunner(ICakeContext context)
        : base(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools)
    {
        _context = context;
    }

    /// <summary>
    /// Runs otool with the specified settings and returns the raw output.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The otool output.</returns>
    public string GetOutput(OtoolSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var arguments = CreateArguments(settings);
        var processOutput = string.Empty;

        Run(settings, arguments, new ProcessSettings
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            process => processOutput = string.Join(Environment.NewLine, process.GetStandardOutput()));

        return processOutput;
    }

    /// <summary>
    /// Runs otool -L and returns a dictionary of library names to their full paths.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>A dictionary mapping library names to their full paths as reported by otool.</returns>
    public IReadOnlyDictionary<string, string> GetDependenciesAsDictionary(OtoolSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Force -L flag for dependency analysis
        var dependencySettings = new OtoolSettings(settings.FilePath)
        {
            ShowLibraries = true,
            Verbose = settings.Verbose
        };

        var rawOutput = GetOutput(dependencySettings);
        return ParseDependenciesFromOutput(rawOutput);
    }

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    /// <returns>The tool name.</returns>
    protected override string GetToolName() => "otool";

    /// <summary>
    /// Gets the possible names of the tool executable.
    /// </summary>
    /// <returns>The tool executable name.</returns>
    protected override IEnumerable<string> GetToolExecutableNames()
    {
        yield return "otool";
    }

    private static ProcessArgumentBuilder CreateArguments(OtoolSettings settings)
    {
        var builder = new ProcessArgumentBuilder();

        if (settings.ShowLibraries)
        {
            builder.Append("-L");
        }

        if (settings.ShowLoadCommands)
        {
            builder.Append("-l");
        }

        if (settings.ShowHeader)
        {
            builder.Append("-h");
        }

        if (settings.Verbose)
        {
            builder.Append("-v");
        }

        builder.AppendQuoted(settings.FilePath.FullPath);

        return builder;
    }

    private ImmutableDictionary<string, string> ParseDependenciesFromOutput(string output)
    {
        var dependencies = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        _context.Debug($"[DEBUG] Total lines in otool output: {lines.Length}");

        // Skip the first line which is typically the file being analyzed
        // Example: "/path/to/libSDL2-2.0.0.dylib:"
        var dependencyLines = lines.Skip(1).ToList();
        _context.Debug($"[DEBUG] Dependency lines to process: {dependencyLines.Count}");

        foreach (var line in dependencyLines)
        {
            _context.Debug($"[DEBUG] Processing line: '{line}'");
            _context.Debug($"[DEBUG] Line length: {line.Length}, IsNullOrWhiteSpace: {string.IsNullOrWhiteSpace(line)}");

            if (string.IsNullOrWhiteSpace(line))
            {
                _context.Debug("[DEBUG] Skipping empty/whitespace line");
                continue;
            }

            var dependency = ParseDependencyLine(line);
            if (dependency != null)
            {
                _context.Debug($"[DEBUG] Successfully parsed dependency: {dependency.FullPath}");
                var libraryName = ExtractLibraryName(dependency.FullPath);
                _context.Debug($"[DEBUG] Extracted library name: {libraryName}");
                dependencies[libraryName] = dependency.FullPath;
            }
            else
            {
                _context.Debug("[DEBUG] Failed to parse dependency from line");
            }
        }

        _context.Debug($"[DEBUG] Final dependencies count: {dependencies.Count}");
        return dependencies.ToImmutableDictionary();
    }

    private DependencyInfo? ParseDependencyLine(string line)
    {
        // Regular expression to parse otool -L output
        // Captures the path and the version information
        _context.Debug($"[DEBUG] ParseDependencyLine input: '{line}'");
        _context.Debug($"[DEBUG] Line bytes: {string.Join(' ', System.Text.Encoding.UTF8.GetBytes(line).Select(b => b.ToString("X2", CultureInfo.CurrentCulture)))}");

        var regex = GetLibraryCompatibilityVersion();
        var match = regex.Match(line);

        _context.Debug($"[DEBUG] Regex match success: {match.Success}");
        switch (match.Success)
        {
            case true:
            {
                _context.Debug($"[DEBUG] Match groups count: {match.Groups.Count}");
                for (var i = 0; i < match.Groups.Count; i++)
                {
                    _context.Debug($"[DEBUG] Group {i}: '{match.Groups[i].Value}'");
                }

                break;
            }
            case false:
                _context.Debug("[DEBUG] Regex match failed");
                return null;
        }

        var fullPath = match.Groups["path"].Value.Trim();
        _context.Debug($"[DEBUG] Extracted full path: '{fullPath}'");
        return new DependencyInfo(fullPath);
    }

    private static string ExtractLibraryName(string fullPath)
    {
        // Handle different types of paths:
        // "/usr/lib/libSystem.B.dylib" → "libSystem.B.dylib"
        // "/System/Library/Frameworks/CoreFoundation.framework/Versions/A/CoreFoundation" → "CoreFoundation.framework"
        // "@rpath/libfoo.dylib" → "libfoo.dylib"
        // "@loader_path/../lib/libbar.dylib" → "libbar.dylib"

        if (!fullPath.Contains(".framework/", StringComparison.Ordinal))
        {
            return Path.GetFileName(fullPath);
        }

        var frameworkMatch = FrameworkNameExtractor().Match(fullPath);

        return frameworkMatch.Success ? frameworkMatch.Groups["framework"].Value : Path.GetFileName(fullPath);
    }

    private sealed record DependencyInfo(string FullPath);
}
