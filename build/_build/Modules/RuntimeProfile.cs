#pragma warning disable S3267

using System.Text.RegularExpressions;
using Build.Context.Models;
using Build.Modules.Contracts;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Modules;

public sealed class RuntimeProfile : IRuntimeProfile
{
    private readonly IReadOnlyList<Regex> _systemRegexes;

    public RuntimeProfile(RuntimeInfo info, SystemArtefactsConfig artefacts, LibraryManifest coreLibManifest)
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
            PlatformFamily.Linux   => artefacts.Linux.SystemLibraries,
            _                      => artefacts.Osx.SystemLibraries,
        };

        _systemRegexes = [.. rawPatterns.Select(BuildRegex)];

        var platformBinaries = coreLibManifest.PrimaryBinaries.FirstOrDefault(x => x.Os.Equals(PlatformFamily.ToString(), StringComparison.OrdinalIgnoreCase));
        CoreLibName = platformBinaries?.Patterns.Count > 0 ? platformBinaries.Patterns[0] : null;
    }

    public string Rid { get; }
    public string Triplet { get; }
    public PlatformFamily PlatformFamily { get; }
    public string? CoreLibName { get; }

    public bool IsSystemFile(FilePath path)
    {
        var fileName = path.GetFilename().FullPath;

        foreach (var rx in _systemRegexes)
        {
            if (rx.IsMatch(fileName))
            {
                return true;
            }
        }

        return false;
    }

    // public bool IsSystemFile(FilePath path)
    // {
    //     ArgumentNullException.ThrowIfNull(path);
    //
    //     var name = path.GetFilename().FullPath;
    //
    //     foreach (var pat in _systemPatterns)
    //     {
    //         if (!pat.Contains('*', StringComparison.Ordinal))
    //         {
    //             if (name.Equals(pat, StringComparison.OrdinalIgnoreCase))
    //             {
    //                 return true;
    //             }
    //         }
    //         else
    //         {
    //             var rx = $"^{Regex.Escape(pat).Replace("\\*", ".*", StringComparison.Ordinal)}$";
    //             if (Regex.IsMatch(name, rx, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(1000)))
    //             {
    //                 return true;
    //             }
    //         }
    //     }
    //
    //     return false;
    // }

    private static Regex BuildRegex(string pattern)
    {
        var regexString = $"^{Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal)}$";

        return new Regex(regexString, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
    }
}
