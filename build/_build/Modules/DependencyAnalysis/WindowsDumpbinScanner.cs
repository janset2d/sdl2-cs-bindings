using Build.Tools.Dumpbin;
using System.Collections.Immutable;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Diagnostics;

namespace Build.Modules.DependencyAnalysis;

public sealed class WindowsDumpbinScanner : IRuntimeScanner
{
    private readonly ICakeContext _context;

    public WindowsDumpbinScanner(ICakeContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlySet<FilePath>> ScanAsync(FilePath binary, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(binary);

        var dumpbinSettings = new DumpbinDependentsSettings(binary.FullPath)
        {
            ToolPath = _context.Tools.Resolve("dumpbin.exe"),
            SetupProcessSettings = settings =>
            {
                settings.RedirectStandardOutput = true;
                settings.RedirectStandardError = true;
            },
        };

        var rawOutput = await Task.Run(() => _context.DumpbinDependents(dumpbinSettings) ?? string.Empty, ct).ConfigureAwait(false);
        var dependentDllNames = ExtractDependentDlls(rawOutput.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries));

        var dependentPaths = new HashSet<FilePath>();
        var binaryDirectory = binary.GetDirectory();

        foreach (var depDllName in dependentDllNames)
        {
            var depDllPath = binaryDirectory.CombineWithFilePath(depDllName);
            if (_context.FileSystem.GetFile(depDllPath).Exists)
            {
                dependentPaths.Add(depDllPath);
            }
            else
            {
                _context.Log.Write(Verbosity.Verbose, LogLevel.Verbose, "Runtime dependency \"{0}\" listed by dumpbin for \"{1}\" not found at \"{2}\".", depDllName, binary.GetFilename(), depDllPath);
            }
        }

        return dependentPaths.ToImmutableHashSet();
    }

    private static IReadOnlyList<string> ExtractDependentDlls(IEnumerable<string> lines)
    {
        const string startMarker = "Image has the following dependencies:";
        const string endMarker = "Summary";
        const string dllSuffix = ".dll";

        return
        [
            .. lines
                .SkipWhile(line => !line.Contains(startMarker, StringComparison.OrdinalIgnoreCase))
                .Skip(1) // Skip the marker line itself
                .TakeWhile(line => !line.Contains(endMarker, StringComparison.OrdinalIgnoreCase))
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line) && line.EndsWith(dllSuffix, StringComparison.OrdinalIgnoreCase)),
        ];
    }
}
