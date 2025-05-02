using Cake.Common.IO;
using Cake.Common.Tools.VSWhere;
using Cake.Common.Tools.VSWhere.Latest;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Build.Vcpkg.Windows;

public class DumpbinDependentsTool : Tool<DumpbinDependentsSettings>
{
    private readonly ICakeContext _cakeContext;
    public const string DumpbinExecutableName = "dumpbin.exe";

    public DumpbinDependentsTool(ICakeContext cakeContext)
        : base(cakeContext.FileSystem, cakeContext.Environment, cakeContext.ProcessRunner, cakeContext.Tools)
    {
        _cakeContext = cakeContext;
    }

    protected override string GetToolName()
    {
        return "Dumpbin";
    }

    protected override IEnumerable<string> GetToolExecutableNames()
    {
        return [DumpbinExecutableName];
    }

    protected override IEnumerable<FilePath> GetAlternativeToolPaths(DumpbinDependentsSettings settings)
    {
        var vsWhereLatest = _cakeContext.VSWhereLatest(new VSWhereLatestSettings()
        {
            Requires = "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
            ReturnProperty = "installationPath",
        });

        var msvcRoot = vsWhereLatest + _cakeContext.Directory(@"VC\Tools\MSVC");

        if (!_cakeContext.DirectoryExists(msvcRoot))
        {
            throw new DirectoryNotFoundException($"MSVC root directory not found: {msvcRoot}");
        }

        // Pick the highest-versioned folder (e.g. 14.43.34808)
        var latestTools = new DirectoryInfo(msvcRoot)
            .EnumerateDirectories()
            .OrderByDescending(d => d.Name, StringComparer.Ordinal)
            .FirstOrDefault();

        if (latestTools == null)
        {
            throw new DirectoryNotFoundException("No MSVC tools directory found.");
        }

        // Host = x64, Target = x64  (adjust if you really need x86 or arm64)

        var dumpbinDir =
            _cakeContext.Directory(latestTools.FullName) +
            _cakeContext.Directory("bin") +
            _cakeContext.Directory("Hostx64") +
            _cakeContext.Directory("x64");

        var dumpbin = dumpbinDir + _cakeContext.File(DumpbinExecutableName);

        if (!_cakeContext.FileExists(dumpbin))
        {
            throw new FileNotFoundException($"Dumpbin executable not found: {dumpbin}");
        }

        return [dumpbin];
    }

    public IList<string>? Dependents(DumpbinDependentsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var builder = new ProcessArgumentBuilder();

        if (!string.IsNullOrWhiteSpace(settings.DllPath))
        {
            builder.Append("/dependents");
            builder.AppendQuoted(settings.DllPath);
        }

        List<string>? dumpbinOut = null;
        Run(settings, builder, new ProcessSettings { RedirectStandardOutput = true },
            process =>
            {
                dumpbinOut = [.. process.GetStandardOutput()];
            });

        return dumpbinOut;
    }
}

public class DumpbinDependentsSettings : ToolSettings
{
    public DumpbinDependentsSettings(string dllPath)
    {
        DllPath = dllPath;
    }

    public string DllPath { get; set; }
}

public static class DumpbinAliases
{
    public static IList<string>? DumpbinDependents(this ICakeContext context, DumpbinDependentsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var tool = new DumpbinDependentsTool(context);

        return tool.Dependents(settings);
    }
}
