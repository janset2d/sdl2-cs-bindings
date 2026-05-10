using System.Text.RegularExpressions;
using Build.Data.Manifest;

namespace Build.Runtime;

public interface IRuntimeProfile
{
    string Rid { get; }

    string Triplet { get; }

    /// <summary>
    /// Build-host-local OS family for this runtime profile, decoupled from Cake's
    /// <c>PlatformFamily</c>. The runtime concept carries no Cake dependencies; Cake-tier
    /// code such as tools and Cake extensions reads <c>ICakePlatform.Family</c> directly.
    /// </summary>
    RuntimeFamily Family { get; }

    /// <summary>
    /// Whether the binary at <paramref name="fileName"/> matches one of the OS-family
    /// system-DLL / shared-object exclusion patterns defined in
    /// <c>manifest.json system_exclusions</c>. Callers that hold a Cake <c>FilePath</c>
    /// should pass <c>path.GetFilename().FullPath</c> here — this method takes a plain
    /// file name to keep the runtime-profile surface Cake-decoupled.
    /// </summary>
    bool IsSystemFile(string fileName);
}

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
    /// <inheritdoc />
    public RuntimeFamily Family { get; }

    /// <inheritdoc />
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
