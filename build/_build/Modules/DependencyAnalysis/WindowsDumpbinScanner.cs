using Build.Tools.Dumpbin;

namespace Build.Modules.DependencyAnalysis;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Diagnostics;

public sealed class WindowsDumpbinScanner : IRuntimeScanner
{
    private readonly ICakeContext _context;

    public WindowsDumpbinScanner(ICakeContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<IReadOnlySet<FilePath>> ScanAsync(FilePath binary, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(binary);

        var dumpbinSettings = new DumpbinDependentsSettings(binary.FullPath)
        {
            ToolPath = _context.Tools.Resolve("dumpbin.exe"), // Example of resolving tool path
            SetupProcessSettings = settings =>
            {
                settings.RedirectStandardOutput = true;
                settings.RedirectStandardError = true;
            },
        };

        // Assuming context.DumpbinDependents returns a string or similar that can be split.
        // This part needs to align with how DumpbinDependents is actually used/called.
        // For this example, let's assume it's a synchronous call that we Task.Run.
        // Ideally, DumpbinDependents itself would be async if it involves external processes.
        var rawOutput = _context.DumpbinDependents(dumpbinSettings) ?? string.Empty;

        // Your existing DumpbinParser logic
        var dependentDllNames = DumpbinParser.ExtractDependentDlls(rawOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));

        var dependentPaths = new HashSet<FilePath>(PathEqualityComparer.Default);
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
        // In a real async method, Task.Run would be used if DumpbinDependents were synchronous.
        // However, IRuntimeScanner.ScanAsync is async, so if DumpbinDependents is sync, it should be wrapped.
        // For simplicity, if DumpbinDependents is sync, this becomes:
        // return Task.FromResult<IReadOnlySet<FilePath>>(dependentPaths.ToImmutableHashSet());
        // If DumpbinDependents were truly async, it'd be: await context.DumpbinDependentsAsync(...)
        return Task.FromResult<IReadOnlySet<FilePath>>(dependentPaths.ToImmutableHashSet(PathEqualityComparer.Default));
    }
}

// Custom PathEqualityComparer as FilePathComparer.Default might not be available or suitable.
public sealed class PathEqualityComparer : IEqualityComparer<FilePath>
{
    public static readonly PathEqualityComparer Default = new();

    public bool Equals(FilePath? x, FilePath? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        // Ensure using OrdinalIgnoreCase for path comparisons on Windows, or adapt for cross-platform needs.
        return string.Equals(x.FullPath, y.FullPath, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(FilePath obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.FullPath);
    }
}
