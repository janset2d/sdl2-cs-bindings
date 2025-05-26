using System.Text.RegularExpressions;
using Build.Context.Models;
using Build.Modules.Contracts;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Modules;

public sealed class RuntimeProfile : IRuntimeProfile
{
    private readonly IReadOnlyList<Regex> _systemRegexes;

    public RuntimeProfile(RuntimeInfo info, SystemArtefactsConfig artefacts)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(artefacts);

        Rid = info.Rid;
        Triplet = info.Triplet;

        if (Rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
        {
            PlatformFamily = PlatformFamily.Windows;
        }
        else if (Rid.StartsWith("osx-", StringComparison.OrdinalIgnoreCase))
        {
            PlatformFamily = PlatformFamily.OSX;
        }
        else if (Rid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase))
        {
            PlatformFamily = PlatformFamily.Linux;
        }
        else
        {
            throw new InvalidOperationException($"Unsupported rid {Rid}");
        }

        var rawPatterns = PlatformFamily switch
        {
            PlatformFamily.Windows => artefacts.Windows.SystemDlls,
            PlatformFamily.Linux => artefacts.Linux.SystemLibraries,
            _ => artefacts.Osx.SystemLibraries,
        };

        _systemRegexes = [.. rawPatterns.Select(BuildRegex)];
    }

    public string Rid { get; }
    public string Triplet { get; }
    public PlatformFamily PlatformFamily { get; }

    public bool IsSystemFile(FilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var fileName = path.GetFilename().FullPath;

        return _systemRegexes.Any(rx => rx.IsMatch(fileName));
    }

    private static Regex BuildRegex(string pattern)
    {
        var regexString = $"^{Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal)}$";

        return new Regex(regexString, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
    }
}
