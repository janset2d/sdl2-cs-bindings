using NuGet.Versioning;

namespace Build.Features.Versioning;

/// <summary>
/// Parses <c>--explicit-version family=semver</c> (repeated) and
/// <c>--explicit-versions family=semver,...</c> (comma-separated) CLI entries into the
/// canonical <see cref="IReadOnlyDictionary{TKey, TValue}"/> mapping consumed by
/// <see cref="ResolveVersionsPipeline"/> when <c>--version-source=explicit</c>.
/// Stage targets do not consume this parser; they read the resolved mapping via
/// <c>--versions-file</c>. Errors surface as <see cref="ArgumentException"/> at CLI
/// binding time so malformed input fails fast before any task runs.
/// </summary>
public static class ExplicitVersionParser
{
    public static IReadOnlyDictionary<string, NuGetVersion> ParseCliEntries(IEnumerable<string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in entries)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            var separatorIndex = trimmed.IndexOf('=', StringComparison.Ordinal);

            if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
            {
                throw new ArgumentException($"--explicit-version entry '{raw}' must be 'family=semver' (e.g., 'sdl2-core=2.32.0').", nameof(entries));
            }

            var family = trimmed[..separatorIndex].Trim();
            var versionLiteral = trimmed[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(family))
            {
                throw new ArgumentException($"--explicit-version entry '{raw}' has an empty family identifier on the left of '='.", nameof(entries));
            }

            if (!NuGetVersion.TryParse(versionLiteral, out var version))
            {
                throw new ArgumentException($"--explicit-version entry '{raw}' has invalid NuGet SemVer '{versionLiteral}' on the right of '='.", nameof(entries));
            }

            if (!mapping.TryAdd(family, version))
            {
                throw new ArgumentException($"--explicit-version entry '{raw}' duplicates family '{family}' (matched case-insensitively).", nameof(entries));
            }
        }

        return mapping;
    }

    /// <summary>
    /// Parses a single comma-separated string of <c>family=semver</c> entries
    /// (e.g. <c>"sdl2-core=2.32.0,sdl2-image=2.8.0"</c>). Exists to eliminate
    /// bash-level IFS parsing in release.yml — the workflow passes
    /// <c>inputs.explicit-versions</c> directly as a single string.
    /// Null or whitespace input returns an empty mapping.
    /// </summary>
    public static IReadOnlyDictionary<string, NuGetVersion> ParseCommaSeparated(string? commaSeparated)
    {
        if (string.IsNullOrWhiteSpace(commaSeparated))
        {
            return new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);
        }

        var segments = commaSeparated.Split(',');
        var entries = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (trimmed.Length > 0)
            {
                entries.Add(trimmed);
            }
        }

        return ParseCliEntries(entries);
    }
}
