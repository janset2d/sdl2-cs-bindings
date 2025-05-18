using System.Text.RegularExpressions;
using Build.Context.Models;
using Build.Modules.Harvesting.Contracts;
using Cake.Core.IO;

namespace Build.Modules.Harvesting;

public sealed class RuntimeProfile : IRuntimeProfile
{
    private readonly IReadOnlyList<string> _systemPatterns;

    public RuntimeProfile(RuntimeInfo info, SystemArtefactsConfig artefacts, LibraryManifest coreLibManifest)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(artefacts);

        Rid = info.Rid;
        Triplet = info.Triplet;

        if (Rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
        {
            OsFamily = "Windows";
        }
        else if (Rid.StartsWith("osx-", StringComparison.OrdinalIgnoreCase))
        {
            OsFamily = "OSX";
        }
        else if (Rid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase))
        {
            OsFamily = "Linux";
        }
        else
        {
            throw new InvalidOperationException($"Unsupported rid {Rid}");
        }

        _systemPatterns = OsFamily switch
        {
            "Windows" => artefacts.Windows.SystemDlls,
            "Linux" => artefacts.Linux.SystemLibraries,
            _ => artefacts.Osx.SystemLibraries,
        };

        CoreLibName = coreLibManifest.LibNames.FirstOrDefault(x => x.Os.Equals(OsFamily, StringComparison.OrdinalIgnoreCase))?.Name;
    }

    public string Rid { get; }
    public string Triplet { get; }
    public string OsFamily { get; }
    public string? CoreLibName { get; }

    public bool IsSystemFile(FilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var name = path.GetFilename().FullPath;

        foreach (var pat in _systemPatterns)
        {
            if (!pat.Contains('*', StringComparison.Ordinal))
            {
                if (name.Equals(pat, StringComparison.OrdinalIgnoreCase)) return true;
            }
            else
            {
                var rx = $"^{Regex.Escape(pat).Replace("\\*", ".*", StringComparison.Ordinal)}$";
                if (Regex.IsMatch(name, rx, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(1000))) return true;
            }
        }

        return false;
    }
}
