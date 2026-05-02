using System.Text.RegularExpressions;
using Build.Shared.Manifest;

namespace Build.Shared.Runtime;

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
            Family = RuntimeFamily.Windows;
        }
        else if (Rid.StartsWith("osx-", StringComparison.OrdinalIgnoreCase))
        {
            Family = RuntimeFamily.OSX;
        }
        else if (Rid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase))
        {
            Family = RuntimeFamily.Linux;
        }
        else
        {
            throw new InvalidOperationException($"Unsupported rid {Rid}");
        }

        var rawPatterns = Family switch
        {
            RuntimeFamily.Windows => artefacts.Windows.SystemDlls,
            RuntimeFamily.Linux => artefacts.Linux.SystemLibraries,
            _ => artefacts.Osx.SystemLibraries,
        };

        _systemRegexes = [.. rawPatterns.Select(BuildRegex)];
    }

    public string Rid { get; }
    public string Triplet { get; }
    public RuntimeFamily Family { get; }

    public bool IsSystemFile(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return _systemRegexes.Any(rx => rx.IsMatch(fileName));
    }

    private static Regex BuildRegex(string pattern)
    {
        var regexString = $"^{Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal)}$";

        return new Regex(regexString, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
    }
}
