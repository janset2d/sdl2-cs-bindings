using Cake.Common.IO;
using Cake.Common.Tools.VSWhere;
using Cake.Common.Tools.VSWhere.Latest;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Build.Tools.Dumpbin;

public abstract class DumpbinTool(ICakeContext cakeContext)
    : Tool<DumpbinSettings>(cakeContext.FileSystem, cakeContext.Environment, cakeContext.ProcessRunner, cakeContext.Tools)
{
    private const string DumpbinExecutableName = "dumpbin.exe";

    protected override string GetToolName()
    {
        return "Dumpbin";
    }

    protected override IEnumerable<string> GetToolExecutableNames()
    {
        return [DumpbinExecutableName];
    }

    protected override IEnumerable<FilePath> GetAlternativeToolPaths(DumpbinSettings settings)
    {
        var vsWhereLatest = cakeContext.VSWhereLatest(new VSWhereLatestSettings()
        {
            Requires = "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
            ReturnProperty = "installationPath",
        });

        var msvcRoot = vsWhereLatest.Combine("VC").Combine("Tools").Combine("MSVC");

        if (!cakeContext.DirectoryExists(msvcRoot))
        {
            throw new DirectoryNotFoundException($"MSVC root directory not found: {msvcRoot}");
        }

        // Pick the highest-versioned folder (e.g. 14.43.34808)
        var latestTools = new DirectoryInfo(msvcRoot.FullPath)
            .EnumerateDirectories()
            .OrderByDescending(d => d.Name, StringComparer.Ordinal)
            .FirstOrDefault();

        if (latestTools == null)
        {
            throw new DirectoryNotFoundException("No MSVC tools directory found.");
        }

        // Host = x64, Target = x64 (adjust if you really need x86 or arm64)
        var dumpbinDir = new DirectoryPath(latestTools.FullName).Combine("bin").Combine("Hostx64").Combine("x64");
        var dumpbin = dumpbinDir.CombineWithFilePath(DumpbinExecutableName);

        if (!cakeContext.FileExists(dumpbin))
        {
            throw new FileNotFoundException($"Dumpbin executable not found: {dumpbin}");
        }

        return [dumpbin];
    }
}
