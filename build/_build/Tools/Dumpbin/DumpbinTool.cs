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
    private const string VsToolsRequirement = "Microsoft.VisualStudio.Component.VC.Tools.x86.x64";

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
        var developerPromptDumpbin = TryResolveFromDeveloperPrompt();
        if (developerPromptDumpbin is not null)
        {
            return [developerPromptDumpbin];
        }

        var vsWhereLatest = cakeContext.VSWhereLatest(new VSWhereLatestSettings()
        {
            Requires = VsToolsRequirement,
            ReturnProperty = "installationPath",
        });

        if (vsWhereLatest is null || string.IsNullOrWhiteSpace(vsWhereLatest.FullPath))
        {
            throw new FileNotFoundException(
                "Dumpbin could not be resolved. Ensure Visual Studio Build Tools with C++ tools is installed, run from a Developer PowerShell/Command Prompt, or add dumpbin.exe to PATH.");
        }

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

        var dumpbin = GetDumpbinCandidates(new DirectoryPath(latestTools.FullName)).FirstOrDefault(cakeContext.FileExists);
        if (dumpbin is null)
        {
            throw new FileNotFoundException($"Dumpbin executable not found under MSVC tools directory: {latestTools.FullName}");
        }

        return [dumpbin];
    }

    private FilePath? TryResolveFromDeveloperPrompt()
    {
        var vcToolsInstallDir = cakeContext.Environment.GetEnvironmentVariable("VCToolsInstallDir");
        if (string.IsNullOrWhiteSpace(vcToolsInstallDir))
        {
            return null;
        }

        var vcToolsDir = new DirectoryPath(vcToolsInstallDir);
        if (!cakeContext.DirectoryExists(vcToolsDir))
        {
            return null;
        }

        return GetDumpbinCandidates(vcToolsDir).FirstOrDefault(cakeContext.FileExists);
    }

    private static IEnumerable<FilePath> GetDumpbinCandidates(DirectoryPath vcToolsDir)
    {
        var binDir = vcToolsDir.Combine("bin");
        yield return binDir.Combine("Hostx64").Combine("x64").CombineWithFilePath(DumpbinExecutableName);
        yield return binDir.Combine("Hostx64").Combine("x86").CombineWithFilePath(DumpbinExecutableName);
        yield return binDir.Combine("Hostx86").Combine("x64").CombineWithFilePath(DumpbinExecutableName);
        yield return binDir.Combine("Hostx86").Combine("x86").CombineWithFilePath(DumpbinExecutableName);
    }
}
